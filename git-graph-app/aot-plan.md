## Plan: Fix AOT Warnings

Goal: remove the listed trim/AOT warnings from git-graph-app without weakening the frontend message contract. Recommended approach: remove the unused AOT-warning package, replace strongly typed SignalR client proxies with untyped SendAsync calls, and replace runtime-reflection JSON with source-generated JSON metadata plus named DTOs.

Principles
- Native AOT cannot generate runtime code or rely on reflection-discovered JSON shapes. Every serialized/deserialized payload must have a stable concrete type and source-generated JsonTypeInfo.
- JsonStringEnumConverter without TEnum is a converter factory. It dynamically discovers enum types, which causes IL3050. Use generic enum converters, custom converters, or enum member-name attributes per enum.
- JsonSerializer Serialize/Deserialize overloads that take only JsonSerializerOptions use reflection metadata. Use overloads that take JsonTypeInfo<T> from a JsonSerializerContext.
- Anonymous objects are unstable for AOT. Replace them with named response/config DTOs so the source generator can emit metadata.
- SignalR Hub<TClient> creates typed client proxies at runtime. This app only needs one client method, ReceiveMessage, so untyped Hub + Clients.SendAsync("ReceiveMessage", ...) keeps behavior and avoids proxy code generation.
- Do not share one JSON options profile everywhere: StateManager intentionally writes indented state and omits null values, while SignalR and /api/initialState must preserve null properties because the frontend checks null versus undefined.

Steps
1. Capture a warning baseline. Run dotnet publish from git-graph-app with Release, win-x64, self-contained, PublishAot=true, TrimmerSingleWarn=false, and SuppressTrimAnalysisWarnings=false. Save/count IL2026, IL3050, IL2104, and IL3053 warnings before editing.

3. Replace strongly typed SignalR with untyped SignalR. Depends on no other step.
- In git-graph-app/Hubs/GitGraphHub.cs, remove IGitGraphClient and change GitGraphHub from Hub<IGitGraphClient> to Hub.
- Update the Send helper from Clients.Caller.ReceiveMessage(response) to Clients.Caller.SendAsync("ReceiveMessage", response).
- In git-graph-app/Program.cs, change IHubContext<GitGraphHub, IGitGraphClient> to IHubContext<GitGraphHub> and send refresh via Clients.All.SendAsync("ReceiveMessage", refreshResponse).
- Keep the frontend unchanged: web/utils.ts already listens for ReceiveMessage.
- Expected result: IL3050 warnings for Hub<T>.Hub(), Hub<T>.Clients get/set, and IHubContext<THub,T>.Clients get are removed instead of suppressed.

4. Create named response DTOs. Depends on step 3 for refresh payload, but can be designed in parallel with step 5.
- Add git-graph-app/Models/ResponseTypes.cs.
- Replace each anonymous Send(new { ... }) and SendResult anonymous payload with named DTO classes/records.
- Reuse stable DTOs for repeated shapes: ErrorResponse(command,error), RepoErrorResponse(command,repo,error), MultiErrorResponse(command,errors), ViewUrlResponse(command,url,error), OpenFileResponse(command,viewUrl,error), RefreshResponse(command).
- Add specific DTOs for unique payloads: LoadCommitsResponse, LoadRepoInfoResponse, LoadReposResponse, LoadConfigResponse, CommitDetailsResponse, CompareCommitsResponse, TagDetailsResponse, AddTagResponse, PushTagResponse, CheckoutBranchResponse, DeleteBranchResponse, PushBranchResponse, CreatePullRequestResponse, MergeResponse, RebaseResponse, StartCodeReviewResponse, CodeReviewIdResponse, SetRepoStateResponse.
- Keep JSON property names and nullability aligned with src/types.ts ResponseMessage. Do not omit null error fields.

5. Create typed initial-state/config DTOs for /api/initialState. Can run in parallel with step 4.
- Add git-graph-app/Models/InitialStateTypes.cs or extend StateTypes.cs if local style favors fewer files.
- Replace GitGraphViewInitialState.Config from object to a concrete GitGraphViewConfig class.
- Mirror the shape in src/types.ts: GitGraphViewInitialState, GitGraphViewConfig, CommitDetailsViewConfig, ContextMenuActionsVisibility, DateFormat, DefaultColumnVisibility, DialogDefaults, GraphConfig, KeybindingConfig, MuteCommitsConfig, OnRepoLoadConfig, ReferenceLabelsConfig, and the /api/initialState envelope that contains initialState, globalState, workspaceState, colorVars, colorParams.
- Preserve current standalone defaults from Program.cs. Keep loadViewTo and issueLinkingConfig as explicit nulls.
- Expected result: Program.cs no longer serializes a deeply nested anonymous response with Results.Json(options).

6. Add source-generated JSON contexts and options helpers. Depends on steps 4 and 5 for final DTO list.
- Add git-graph-app/Models/Serialization/GitGraphPayloadJsonContext.cs for SignalR and HTTP payloads.
- Add git-graph-app/Models/Serialization/GitGraphStateJsonContext.cs for StateManager persisted state.
- Add git-graph-app/Models/Serialization/GitGraphJsonOptions.cs if needed to centralize custom/generic enum converter setup.
- Payload context settings: camelCase property names, case-insensitive reads, no DefaultIgnoreCondition, no WriteIndented. Include RequestMessage, all request derived classes from MessageTypes.cs, all response DTOs, initial-state/config DTOs, GitTypes, StateTypes, dictionary/array wrapper types used by payloads, and string[].
- State context settings: camelCase property names, DefaultIgnoreCondition.WhenWritingNull, WriteIndented=true. Include PersistedState and all nested state types, including Dictionary<string,GitRepoState> and Dictionary<string,Dictionary<string,CodeReviewData>>.
- Register existing custom converters for GitFileStatus, GitSignatureStatus, MergeActionOn, and RebaseActionOn.
- Replace the non-generic JsonStringEnumConverter with AOT-safe enum handling. Prefer exact-value custom converters or JsonStringEnumMemberName attributes for enums whose TypeScript values are not simple camelCase, especially GitPushBranchMode, CommitOrdering, and RepoCommitOrdering. Use JsonStringEnumConverter<TEnum>(JsonNamingPolicy.CamelCase) only where it preserves existing wire/state values.

7. Update JSON call sites to use JsonTypeInfo.
- In StateManager.cs, replace JsonSerializer.Deserialize<PersistedState>(json, JsonOptions) with the JsonTypeInfo overload from GitGraphStateJsonContext.
- In StateManager.cs, replace JsonSerializer.Serialize(_state, JsonOptions) with the JsonTypeInfo overload from GitGraphStateJsonContext.
- In GitGraphHub.cs, remove the reflection-based JsonOptions field or replace it with payload options tied to GitGraphPayloadJsonContext.
- Change Deserialize<T>(JsonElement) to Deserialize<T>(JsonElement, JsonTypeInfo<T>) and remove the new() fallback. Each switch case passes the matching GitGraphPayloadJsonContext.Default.RequestX type info.
- In GitService.cs, serialize the missing remote string[] with GitGraphPayloadJsonContext.Default.StringArray or equivalent JsonTypeInfo<string[]>.
- In Program.cs, configure AddJsonProtocol payload serializer options with the payload context/options.
- In Program.cs /api/initialState, return the typed response using a JsonTypeInfo overload if available; otherwise write via HttpResponse.WriteAsJsonAsync(response, GitGraphPayloadJsonContext.Default.InitialStateResponse) to avoid Results.Json(JsonSerializerOptions).

8. Compile and fix remaining source-generation gaps. Depends on steps 2-7.
- Build/publish will reveal missing JsonSerializable attributes as runtime metadata errors or remaining IL warnings.
- Add missing concrete DTOs, arrays, nullable wrapper types, dictionaries, and enum converters to the contexts until JSON warnings are gone.
- If any SignalR warning remains from framework internals after removing Hub<TClient>, confirm whether it is from app code or a framework/package grouping warning before deciding on a narrow suppression.

9. Verification.
- Run dotnet build in git-graph-app.
- Run dotnet publish -c Release -r win-x64 --self-contained -p:PublishAot=true -p:TrimmerSingleWarn=false -p:SuppressTrimAnalysisWarnings=false from git-graph-app.
- Confirm no IL2026/IL3050 warnings remain for System.Text.Json, JsonStringEnumConverter, StateManager, GitGraphHub, GitService, or Program.
- Allow IL2104/IL3053 warning remains for Drk.AspNetCore.MinimalApiKit.
- Run the published GitGraphApp.exe with a local repo path, open the served UI, and manually test initial load, repo refresh, load commits, commit details, tag details, branch/tag operations that return errors, diff/file view, and code review start/update/end.
- Check %USERPROFILE%/.git-graph/state.json after state writes: it should remain camelCase, indented, and omit nulls only for the persisted state file.

Relevant files
- git-graph-app/Program.cs — SignalR setup, watcher refresh payload, /api/initialState response, app.RunAsDesktopTool replacement.
- git-graph-app/Hubs/GitGraphHub.cs — Hub<TClient> removal, Send helper, request deserialization, anonymous response DTO replacement.
- git-graph-app/Services/StateManager.cs — persisted-state source-generated serialize/deserialize.
- git-graph-app/Services/GitService.cs — string[] JSON serialization in PushTagAsync.
- git-graph-app/Models/Enums.cs — existing custom converters plus exact AOT-safe enum handling.
- git-graph-app/Models/MessageTypes.cs — request types included in payload context.
- git-graph-app/Models/GitTypes.cs — git data types included in payload context.
- git-graph-app/Models/StateTypes.cs — state types and GitGraphViewInitialState update.
- git-graph-app/Models/ResponseTypes.cs — new response DTOs.
- git-graph-app/Models/InitialStateTypes.cs — new config/initial-state DTOs if not kept in StateTypes.cs.
- git-graph-app/Models/Serialization/GitGraphPayloadJsonContext.cs — new source-generated payload context.
- git-graph-app/Models/Serialization/GitGraphStateJsonContext.cs — new source-generated persisted-state context.
- src/types.ts and web/utils.ts — reference only; do not change unless C# DTO discovery exposes an existing contract mismatch.

Decisions
- Recommended route fixes warnings at their source where practical. It avoids broad suppressions.
- Strongly typed SignalR is deliberately removed because it is the source of proxy-generation IL3050 warnings and adds little value with a single ReceiveMessage method.
- Null preservation for SignalR/HTTP payloads is non-negotiable because frontend code distinguishes null from undefined.
- StateManager keeps its existing persisted-file behavior by using a separate state context.
- Allow MinimalApiKit warning

Further considerations
1. Enum wire values need a careful pass. Some TypeScript enums are string-valued with hyphenated or special values, so exact converters/JsonStringEnumMemberName attributes are safer than blindly using camelCase generic enum converters.
2. If the implementation wants the smallest first PR, split into: package + SignalR removal, JSON contexts for StateManager/GitService, then DTO/source-gen for Hub and initialState.
3. If a framework-level SignalR warning remains after untyped Hub conversion, document and suppress only that narrow warning with a justification comment, after proving it is not from app-owned serialization or typed proxy code.