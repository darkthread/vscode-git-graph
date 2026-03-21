/**
 * server.ts — Git Graph Standalone Backend
 *
 * Starts an Express HTTP server that:
 *  • Serves the static frontend assets (index.html, out.min.js, out.min.css, theme.css)
 *  • Exposes GET /api/initialState for the bootstrap page
 *  • Exposes GET /api/file and GET /api/diff for in-browser file / diff viewing
 *  • Manages a WebSocket endpoint /ws that mirrors the VS Code webview message protocol
 *
 * The vscode module is shimmed before any source file is imported so that
 * DataSource, Config, Logger etc. can run without the VS Code extension host.
 *
 * Start with:
 *   ts-node -r ./register.ts server.ts [repo-path1] [repo-path2] ...
 *   (or after compiling: node -r ./register.js server.js [repo-path1] ...)
 */

// ── MUST be first: register the vscode shim ─────────────────────────────────
// When this file is run via `node -r ./register.js server.js`, the shim is
// already in place.  When run directly with ts-node, require it here.
if (!require.cache[require.resolve('./register')]) {
	// eslint-disable-next-line @typescript-eslint/no-var-requires
	require('./register');
}

// ── Standard & third-party imports ──────────────────────────────────────────
import * as fs from 'fs';
import * as http from 'http';
import * as os from 'os';
import * as path from 'path';
import * as cp from 'child_process';
// Using require() to avoid @types/express and @types/ws which require TypeScript >= 4.1
// eslint-disable-next-line @typescript-eslint/no-var-requires
const express: any = require('express');
// eslint-disable-next-line @typescript-eslint/no-var-requires
const { WebSocketServer, WebSocket } = require('ws') as { WebSocketServer: any; WebSocket: any; };

// ── Git Graph source imports (safe after vscode shim) ───────────────────────
import { DataSource, GitConfigKey } from '../src/dataSource';
import { Logger } from '../src/logger';
import { EventEmitter } from '../src/utils/event';
import { GitExecutable, getGitExecutable } from '../src/utils';
import {
	BooleanOverride,
	CommitDetailsViewLocation,
	CommitOrdering,
	ContextMenuActionsVisibility,
	DateFormatType,
	DateType,
	FileViewType,
	GitFileStatus,
	GitGraphViewGlobalState,
	GitGraphViewInitialState,
	GitGraphViewWorkspaceState,
	GitPushBranchMode,
	GraphStyle,
	GraphUncommittedChangesStyle,
	PullRequestConfig,
	PullRequestProvider,
	RebaseActionOn,
	RequestMessage,
	ResponseMessage
} from '../src/types';
import { StateManager, DEFAULT_REPO_STATE } from './stateManager';

// ── Config helpers (standalone defaults) ────────────────────────────────────
const DEFAULT_GRAPH_COLOURS = [
	'#0085d9', '#d9008f', '#00d90a', '#d98500', '#a300d9',
	'#ff0000', '#00d9cc', '#e138e8', '#85d900', '#dc5b23',
	'#6f24d6', '#ffcc00'
];

function buildDefaultConfig(): GitGraphViewInitialState['config'] {
	return {
		commitDetailsView: {
			autoCenter: true,
			fileViewType: FileViewType.Tree,
			location: CommitDetailsViewLocation.Inline
		},
		commitOrdering: CommitOrdering.Date,
		contextMenuActionsVisibility: {
			branch: {
				checkout: true, rename: true, delete: true, rebase: true,
				merge: true, rebase_i: true, push: true, viewIssue: true,
				createPullRequest: true, createArchive: true, copyName: true
			},
			commit: {
				addTag: true, createBranch: true, viewDetails: true, resetToCommit: true,
				resetToCommit_hard: true, resetToCommit_mixed: true, resetToCommit_soft: true,
				cherrypickCommit: true, revertCommit: true, dropCommit: true, stashUncommittedChanges: true,
				createArchive: true, copyHash: true, copySubject: true
			},
			commitDetailsViewFile: {
				viewDiff: true, viewFileAtThisRevision: true, viewDiffWithWorkingFile: true,
				openFile: true, markAsReviewed: true, markAsNotReviewed: true,
				resetFileToThisRevision: true, copyAbsoluteFilePath: true, copyRelativeFilePath: true
			},
			remoteBranch: {
				checkout: true, delete: true, fetch: true, merge: true, pull: true,
				rebase: true, rebase_i: true, createPullRequest: true, createArchive: true, copyName: true
			},
			stash: {
				apply: true, createBranch: true, pop: true, drop: true, copyName: true, copyHash: true
			},
			tag: {
				viewDetails: true, delete: true, push: true, createArchive: true, copyName: true
			}
		} as unknown as ContextMenuActionsVisibility,
		customBranchGlobPatterns: [],
		customEmojiShortcodeMappings: [],
		customPullRequestProviders: [],
		dateFormat: { type: DateFormatType.DateAndTime, iso: false },
		defaultColumnVisibility: { date: true, author: true, commit: true },
		dialogDefaults: {
			addTag: { pushToRemote: false, type: 'annotated' },
			applyStash: { reinstateIndex: false },
			cherryPick: { noCommit: false, recordOrigin: false },
			createBranch: { checkout: false },
			deleteBranch: { forceDelete: false },
			fetchIntoLocalBranch: { forceFetch: false },
			fetchRemote: { prune: false, pruneTags: false },
			general: { referenceInputSpaceSubstitution: 'None' },
			merge: { noCommit: false, rebase: false, squashCommits: false },
			popStash: { reinstateIndex: false },
			pullBranch: { noCommit: false, rebase: false, squashCommits: false },
			rebase: { ignoreDate: true, launchInteractiveRebase: false },
			resetCurrentBranchToCommit: { mode: 'mixed' },
			resetUncommittedChanges: { mode: 'mixed' },
			stashUncommittedChanges: { includeUntracked: true }
		} as any,
		enhancedAccessibility: false,
		fetchAndPrune: false,
		fetchAndPruneTags: false,
		fetchAvatars: false,
		graph: {
			colours: DEFAULT_GRAPH_COLOURS,
			style: GraphStyle.Rounded,
			grid: { x: 16, y: 24, offsetX: 16, offsetY: 12, expandY: 250 },
			uncommittedChanges: GraphUncommittedChangesStyle.OpenCircleAtTheUncommittedChanges
		},
		includeCommitsMentionedByReflogs: false,
		initialLoadCommits: 300,
		keybindings: { find: 'f', refresh: 'r', scrollToHead: 'h', scrollToStash: 's' },
		loadMoreCommits: 75,
		loadMoreCommitsAutomatically: true,
		markdown: true,
		mute: { commitsThatAreNotAncestorsOfHead: false, mergeCommits: true },
		onlyFollowFirstParent: false,
		onRepoLoad: { scrollToHead: false, showCheckedOutBranch: false, showSpecificBranches: [] },
		referenceLabels: {
			alignment: 'Normal',
			branchLabelsAlignedToGraph: false,
			combineLocalAndRemoteBranchLabels: true,
			tagLabelsOnRight: false
		},
		repoDropdownOrder: 'Full Path',
		showRemoteBranches: true,
		showStashes: true,
		showTags: true
	} as any;
}

/* ------------------------------------------------------------------ */
/*  Repo discovery                                                       */
/* ------------------------------------------------------------------ */

async function isGitRepo(dirPath: string): Promise<boolean> {
	return new Promise((resolve) => {
		cp.exec('git -C ' + JSON.stringify(dirPath) + ' rev-parse --is-inside-work-tree', (err) => {
			resolve(!err);
		});
	});
}

async function discoverRepos(roots: string[]): Promise<string[]> {
	const repos: string[] = [];
	for (const root of roots) {
		if (await isGitRepo(root)) {
			// Normalize to forward slashes (same as extension does via vscode.Uri.file)
			repos.push(path.normalize(root).replace(/\\/g, '/'));
		}
	}
	return repos;
}

/* ------------------------------------------------------------------ */
/*  Simple fs.watch-based repo watcher (replaces RepoFileWatcher)       */
/* ------------------------------------------------------------------ */

const FILE_CHANGE_REGEX = /(^\.git\/(config|index|HEAD|refs\/stash|refs\/heads\/.*|refs\/remotes\/.*|refs\/tags\/.*)$)|(^(?!\.git).*$)|(^\.git[^\/]+$)/;

class SimpleRepoWatcher {
	private watcher: fs.FSWatcher | null = null;
	private muted = false;
	private resumeAt = 0;
	private timeout: NodeJS.Timer | null = null;

	constructor(private readonly onChange: () => void) {}

	start(repo: string) {
		this.stop();
		try {
			this.watcher = fs.watch(repo, { recursive: true }, (_event, filename) => {
				if (filename && FILE_CHANGE_REGEX.test(filename.replace(/\\/g, '/'))) {
					this.scheduleRefresh();
				}
			});
		} catch { /* watch may fail on some platforms */ }
	}

	stop() {
		if (this.watcher) { this.watcher.close(); this.watcher = null; }
		if (this.timeout) { clearTimeout(this.timeout as any); this.timeout = null; }
	}

	mute() { this.muted = true; }

	unmute() {
		this.muted = false;
		this.resumeAt = Date.now() + 1500;
	}

	private scheduleRefresh() {
		if (this.muted || Date.now() < this.resumeAt) return;
		if (this.timeout) clearTimeout(this.timeout as any);
		this.timeout = setTimeout(() => {
			this.timeout = null;
			if (!this.muted) this.onChange();
		}, 750);
	}
}

/* ------------------------------------------------------------------ */
/*  createPullRequest helper (server side)                              */
/* ------------------------------------------------------------------ */

function buildPullRequestUrl(config: PullRequestConfig, sourceOwner: string, sourceRepo: string, sourceBranch: string): string {
	let templateUrl: string;
	switch (config.provider) {
		case PullRequestProvider.Bitbucket:
			templateUrl = '$1/$2/$3/pull-requests/new?source=$2/$3::$4&dest=$5/$6::$8'; break;
		case PullRequestProvider.Custom:
			templateUrl = (config as any).custom?.templateUrl ?? ''; break;
		case PullRequestProvider.GitHub:
			templateUrl = '$1/$5/$6/compare/$8...$2:$4'; break;
		case PullRequestProvider.GitLab:
			templateUrl = '$1/$2/$3/-/merge_requests/new?merge_request[source_branch]=$4&merge_request[target_branch]=$8' +
				(config.destProjectId !== '' ? '&merge_request[target_project_id]=$7' : ''); break;
		default:
			return '';
	}
	const fields = [
		config.hostRootUrl,
		sourceOwner, sourceRepo, sourceBranch,
		config.destOwner, config.destRepo, config.destProjectId, config.destBranch
	];
	return templateUrl.replace(/\$([1-8])/g, (_, i) => fields[parseInt(i) - 1] ?? '');
}

/* ------------------------------------------------------------------ */
/*  Main bootstrap                                                       */
/* ------------------------------------------------------------------ */

async function main() {
	/* --- Find git executable --- */
	let gitExecutable: GitExecutable | null = null;
	try {
		gitExecutable = await getGitExecutable('git');
	} catch {
		console.warn('[server] Could not locate git on PATH – git operations will fail.');
	}

	/* --- Core singletons --- */
	const logger = new Logger();
	const configChangeEmitter = new EventEmitter<any>();
	const gitExecChangeEmitter = new EventEmitter<GitExecutable>();
	const stateManager = new StateManager();

	const dataSource = new DataSource(
		gitExecutable,
		configChangeEmitter.subscribe,
		gitExecChangeEmitter.subscribe,
		logger
	);

	/* --- Discover repos from CLI args, or current directory --- */
	const cliRepos = process.argv.slice(2);
	const repoRoots = cliRepos.length > 0 ? cliRepos : [process.cwd()];
	const repoList = await discoverRepos(repoRoots);

	// Ensure all discovered repos are registered in state.
	// Also purge any stale entries whose paths are relative or no longer valid.
	const existingRepos = stateManager.getRepos();
	for (const key of Object.keys(existingRepos)) {
		if (!path.isAbsolute(key)) {
			delete existingRepos[key];
		}
	}
	for (const repo of repoList) {
		if (!existingRepos[repo]) {
			existingRepos[repo] = { ...DEFAULT_REPO_STATE };
		}
	}
	stateManager.saveRepos(existingRepos);

	/* --- Express setup --- */
	const app = express();
	// __dirname at runtime = <repo>/out/backend/backend — go up 3 levels to repo root
	const REPO_ROOT = path.join(__dirname, '..', '..', '..');
	const STATIC_DIR = path.join(REPO_ROOT, 'media');

	// Serve compiled frontend assets (out.min.js, out.min.css, theme.css)
	app.use(express.static(STATIC_DIR));
	// Serve index.html at root
	app.get('/', (_req, res) => {
		res.sendFile(path.join(REPO_ROOT, 'web', 'index.html'));
	});

	/* GET /api/initialState — all data needed before out.min.js runs */
	app.get('/api/initialState', (_req, res) => {
		const repos = stateManager.getRepos();
		const config = buildDefaultConfig();

		let colorVars = '', colorParams = '';
		for (let i = 0; i < config.graph.colours.length; i++) {
			colorVars += '--git-graph-color' + i + ':' + config.graph.colours[i] + '; ';
			colorParams += '[data-color="' + i + '"]{--git-graph-color:var(--git-graph-color' + i + ');} ';
		}

		const initialState: GitGraphViewInitialState = {
			config,
			lastActiveRepo: stateManager.getLastActiveRepo(),
			loadViewTo: null,
			repos,
			loadRepoInfoRefreshId: 0,
			loadCommitsRefreshId: 0
		};

		res.json({
			initialState,
			globalState:    stateManager.getGlobalViewState(),
			workspaceState: stateManager.getWorkspaceViewState(),
			colorVars,
			colorParams
		});
	});

	/* GET /api/file — return file content at a git revision */
	app.get('/api/file', async (req, res) => {
		const { repo, hash, filePath } = req.query as Record<string, string>;
		if (!repo || !hash || !filePath) {
			return res.status(400).send('Missing repo, hash or filePath');
		}
		try {
			const content = await dataSource.getCommitFile(repo, hash, filePath);
			res.type(path.extname(filePath) || '.txt').send(content);
		} catch (err: any) {
			res.status(404).send(err?.toString() ?? 'Not found');
		}
	});

	/* GET /api/diff — return unified diff between two commits/files */
	app.get('/api/diff', async (req, res) => {
		const { repo, fromHash, toHash, oldPath, newPath } = req.query as Record<string, string>;
		if (!repo || !fromHash || !toHash) {
			return res.status(400).send('Missing required parameters');
		}
		const from = fromHash === '*' ? '' : fromHash;
		const to   = toHash   === '*' ? '' : toHash;
		const args = ['diff', '--no-color', from + (to ? '..' + to : ''),
			'--', oldPath ?? newPath, newPath ?? oldPath].filter(Boolean);

		cp.execFile(
			gitExecutable?.path ?? 'git', args,
			{ cwd: repo, maxBuffer: 10 * 1024 * 1024 },
			(err, stdout, stderr) => {
				if (err && !stdout) return res.status(500).send(stderr);
				res.type('text/plain').send(stdout);
			}
		);
	});

	/* --- HTTP + WebSocket server --- */
	const server = http.createServer(app);
	const wss = new WebSocketServer({ server });

	wss.on('connection', (ws: any) => {
		let currentRepo: string | null = null;
		const watcher = new SimpleRepoWatcher(() => {
			send({ command: 'refresh' } as any);
		});

		function send(msg: ResponseMessage | { command: string }) {
			if (ws.readyState === WebSocket.OPEN) {
				ws.send(JSON.stringify(msg));
			}
		}

		ws.on('message', async (data: Buffer) => {
			let msg: RequestMessage;
			try { msg = JSON.parse(data.toString()); }
			catch { return; }

			watcher.mute();
			let errorInfos: any[];

			try {
				switch (msg.command) {

					/* ── Read-only data queries ─────────────────────────── */

					case 'loadCommits':
						send({
							command: 'loadCommits',
							refreshId: msg.refreshId,
							onlyFollowFirstParent: msg.onlyFollowFirstParent,
							...await dataSource.getCommits(
								msg.repo, msg.branches, msg.maxCommits,
								msg.showTags, msg.showRemoteBranches,
								msg.includeCommitsMentionedByReflogs,
								msg.onlyFollowFirstParent,
								msg.commitOrdering, msg.remotes,
								msg.hideRemotes, msg.stashes
							)
						});
						break;

					case 'loadRepoInfo': {
						if (msg.repo !== currentRepo) {
							currentRepo = msg.repo;
							stateManager.setLastActiveRepo(msg.repo);
							watcher.start(msg.repo);
						}
						let repoInfo = await dataSource.getRepoInfo(
							msg.repo, msg.showRemoteBranches, msg.showStashes, msg.hideRemotes
						);
						let isRepo = true;
						if (repoInfo.error) {
							isRepo = (await dataSource.repoRoot(msg.repo)) !== null;
							if (!isRepo) repoInfo.error = null;
						}
						send({ command: 'loadRepoInfo', refreshId: msg.refreshId, ...repoInfo, isRepo });
						break;
					}

					case 'loadRepos':
					// Mirror extension behaviour: always respond, even when check:true
					// (check:true is sent when isRepo===false; we respond with current repos
					// so the frontend can recover and show the "no repos" state cleanly).
					send({
						command: 'loadRepos',
						repos: stateManager.getRepos(),
						lastActiveRepo: stateManager.getLastActiveRepo(),
						loadViewTo: null
					} as any);
					break;

				case 'loadConfig':
						send({
							command: 'loadConfig',
							repo: msg.repo,
							...await dataSource.getConfig(msg.repo, msg.remotes)
						});
						break;

					case 'commitDetails': {
						const UNCOMMITTED = '*';
						const [detailsData, avatar] = await Promise.all([
							msg.commitHash === UNCOMMITTED
								? dataSource.getUncommittedDetails(msg.repo)
								: msg.stash === null
									? dataSource.getCommitDetails(msg.repo, msg.commitHash, msg.hasParents)
									: dataSource.getStashDetails(msg.repo, msg.commitHash, msg.stash),
							Promise.resolve(null) // no avatar manager in standalone mode
						]);
						send({
							command: 'commitDetails',
							...detailsData,
							avatar,
							codeReview: msg.commitHash !== UNCOMMITTED
								? stateManager.getCodeReview(msg.repo, msg.commitHash)
								: null,
							refresh: msg.refresh
						});
						break;
					}

					case 'compareCommits': {
						const UNCOMMITTED = '*';
						send({
							command: 'compareCommits',
							commitHash: msg.commitHash,
							compareWithHash: msg.compareWithHash,
							...await dataSource.getCommitComparison(msg.repo, msg.fromHash, msg.toHash),
							codeReview: msg.toHash !== UNCOMMITTED
								? stateManager.getCodeReview(msg.repo, msg.fromHash + '-' + msg.toHash)
								: null,
							refresh: msg.refresh
						});
						break;
					}

					case 'tagDetails':
						send({
							command: 'tagDetails',
							tagName: msg.tagName,
							commitHash: msg.commitHash,
							...await dataSource.getTagDetails(msg.repo, msg.tagName)
						});
						break;

					/* ── File viewing (redirect to HTTP API) ───────────── */

					case 'viewDiff': {
						const url = '/api/diff?repo=' + encodeURIComponent(msg.repo) +
							'&fromHash=' + encodeURIComponent(msg.fromHash) +
							'&toHash=' + encodeURIComponent(msg.toHash) +
							'&oldPath=' + encodeURIComponent(msg.oldFilePath) +
							'&newPath=' + encodeURIComponent(msg.newFilePath);
						// error:null closes the action spinner; viewUrl is intercepted by utils.ts
						send({ command: 'viewDiff', error: null, viewUrl: url } as any);
						break;
					}

					case 'viewDiffWithWorkingFile': {
						const url = '/api/diff?repo=' + encodeURIComponent(msg.repo) +
							'&fromHash=' + encodeURIComponent(msg.hash) +
							'&toHash=*&oldPath=' + encodeURIComponent(msg.filePath) +
							'&newPath=' + encodeURIComponent(msg.filePath);
						send({ command: 'viewDiffWithWorkingFile', error: null, viewUrl: url } as any);
						break;
					}

					case 'viewFileAtRevision': {
						const url = '/api/file?repo=' + encodeURIComponent(msg.repo) +
							'&hash=' + encodeURIComponent(msg.hash) +
							'&filePath=' + encodeURIComponent(msg.filePath);
						send({ command: 'viewFileAtRevision', error: null, viewUrl: url } as any);
						break;
					}

					case 'openFile': {
						const url = '/api/file?repo=' + encodeURIComponent(msg.repo) +
							'&hash=' + encodeURIComponent(msg.hash ?? 'HEAD') +
							'&filePath=' + encodeURIComponent(msg.filePath);
						send({ command: 'openFile', error: null, viewUrl: url } as any);
						break;
					}

					/* ── Git write operations ───────────────────────────── */

					case 'addRemote':
						send({ command: 'addRemote', error: await dataSource.addRemote(msg.repo, msg.name, msg.url, msg.pushUrl, msg.fetch) });
						break;

					case 'addTag':
						errorInfos = [await dataSource.addTag(msg.repo, msg.tagName, msg.commitHash, msg.type, msg.message, msg.force)];
						if (errorInfos[0] === null && msg.pushToRemote !== null) {
							errorInfos.push(...await dataSource.pushTag(msg.repo, msg.tagName, [msg.pushToRemote], msg.commitHash, msg.pushSkipRemoteCheck));
						}
						send({ command: 'addTag', repo: msg.repo, tagName: msg.tagName, pushToRemote: msg.pushToRemote, commitHash: msg.commitHash, errors: errorInfos });
						break;

					case 'applyStash':
						send({ command: 'applyStash', error: await dataSource.applyStash(msg.repo, msg.selector, msg.reinstateIndex) });
						break;

					case 'branchFromStash':
						send({ command: 'branchFromStash', error: await dataSource.branchFromStash(msg.repo, msg.selector, msg.branchName) });
						break;

					case 'checkoutBranch':
						errorInfos = [await dataSource.checkoutBranch(msg.repo, msg.branchName, msg.remoteBranch)];
						if (errorInfos[0] === null && msg.pullAfterwards !== null) {
							errorInfos.push(await dataSource.pullBranch(msg.repo, msg.pullAfterwards.branchName, msg.pullAfterwards.remote, msg.pullAfterwards.createNewCommit, msg.pullAfterwards.squash));
						}
						send({ command: 'checkoutBranch', pullAfterwards: msg.pullAfterwards, errors: errorInfos });
						break;

					case 'checkoutCommit':
						send({ command: 'checkoutCommit', error: await dataSource.checkoutCommit(msg.repo, msg.commitHash) });
						break;

					case 'cherrypickCommit':
						errorInfos = [await dataSource.cherrypickCommit(msg.repo, msg.commitHash, msg.parentIndex, msg.recordOrigin, msg.noCommit)];
						send({ command: 'cherrypickCommit', errors: errorInfos });
						break;

					case 'cleanUntrackedFiles':
						send({ command: 'cleanUntrackedFiles', error: await dataSource.cleanUntrackedFiles(msg.repo, msg.directories) });
						break;

					case 'createBranch':
						send({ command: 'createBranch', errors: await dataSource.createBranch(msg.repo, msg.branchName, msg.commitHash, msg.checkout, msg.force) });
						break;

					case 'createPullRequest': {
						errorInfos = [msg.push ? await dataSource.pushBranch(msg.repo, msg.sourceBranch, msg.sourceRemote, true, GitPushBranchMode.Normal) : null];
						if (errorInfos[0] === null) {
							const url = buildPullRequestUrl(msg.config, msg.sourceOwner, msg.sourceRepo, msg.sourceBranch);
							if (url) errorInfos.push(null); // success — frontend will open the URL via openExternalUrl
							else errorInfos.push('Could not build Pull Request URL for this provider.');
						}
						send({ command: 'createPullRequest', push: msg.push, errors: errorInfos });
						break;
					}

					case 'deleteBranch':
						errorInfos = [await dataSource.deleteBranch(msg.repo, msg.branchName, msg.forceDelete)];
						if (errorInfos[0] === null) {
							for (const remote of msg.deleteOnRemotes) {
								errorInfos.push(await dataSource.deleteRemoteBranch(msg.repo, msg.branchName, remote));
							}
						}
						send({ command: 'deleteBranch', repo: msg.repo, branchName: msg.branchName, deleteOnRemotes: msg.deleteOnRemotes, errors: errorInfos });
						break;

					case 'deleteRemote':
						send({ command: 'deleteRemote', error: await dataSource.deleteRemote(msg.repo, msg.name) });
						break;

					case 'deleteRemoteBranch':
						send({ command: 'deleteRemoteBranch', error: await dataSource.deleteRemoteBranch(msg.repo, msg.branchName, msg.remote) });
						break;

					case 'deleteTag':
						send({ command: 'deleteTag', error: await dataSource.deleteTag(msg.repo, msg.tagName, msg.deleteOnRemote) });
						break;

					case 'deleteUserDetails': {
						errorInfos = [];
						if (msg.name) errorInfos.push(await dataSource.unsetConfigValue(msg.repo, GitConfigKey.UserName, msg.location));
						if (msg.email) errorInfos.push(await dataSource.unsetConfigValue(msg.repo, GitConfigKey.UserEmail, msg.location));
						send({ command: 'deleteUserDetails', errors: errorInfos });
						break;
					}

					case 'dropCommit':
						send({ command: 'dropCommit', error: await dataSource.dropCommit(msg.repo, msg.commitHash) });
						break;

					case 'dropStash':
						send({ command: 'dropStash', error: await dataSource.dropStash(msg.repo, msg.selector) });
						break;

					case 'editRemote':
						send({ command: 'editRemote', error: await dataSource.editRemote(msg.repo, msg.nameOld, msg.nameNew, msg.urlOld, msg.urlNew, msg.pushUrlOld, msg.pushUrlNew) });
						break;

					case 'editUserDetails': {
						errorInfos = [
							await dataSource.setConfigValue(msg.repo, GitConfigKey.UserName, msg.name, msg.location),
							await dataSource.setConfigValue(msg.repo, GitConfigKey.UserEmail, msg.email, msg.location)
						];
						send({ command: 'editUserDetails', errors: errorInfos });
						break;
					}

					case 'endCodeReview':
						stateManager.endCodeReview(msg.repo, msg.id);
						break;

					case 'exportRepoConfig':
						send({ command: 'exportRepoConfig', error: 'Export repo config is not supported in standalone mode.' });
						break;

					case 'fetch':
						send({ command: 'fetch', error: await dataSource.fetch(msg.repo, msg.name, msg.prune, msg.pruneTags) });
						break;

					case 'fetchAvatar':
						// Avatar fetching not implemented in standalone mode
						break;

					case 'fetchIntoLocalBranch':
						send({ command: 'fetchIntoLocalBranch', error: await dataSource.fetchIntoLocalBranch(msg.repo, msg.remote, msg.remoteBranch, msg.localBranch, msg.force) });
						break;

					case 'merge':
						send({ command: 'merge', actionOn: msg.actionOn, error: await dataSource.merge(msg.repo, msg.obj, msg.actionOn, msg.createNewCommit, msg.squash, msg.noCommit) });
						break;

					case 'openExternalDirDiff':
						send({ command: 'openExternalDirDiff', error: await dataSource.openExternalDirDiff(msg.repo, msg.fromHash, msg.toHash, msg.isGui) });
						break;

					case 'openTerminal':
						send({ command: 'openTerminal', error: 'Integrated terminal is not available in standalone mode.' });
						break;

					case 'popStash':
						send({ command: 'popStash', error: await dataSource.popStash(msg.repo, msg.selector, msg.reinstateIndex) });
						break;

					case 'pruneRemote':
						send({ command: 'pruneRemote', error: await dataSource.pruneRemote(msg.repo, msg.name) });
						break;

					case 'pullBranch':
						send({ command: 'pullBranch', error: await dataSource.pullBranch(msg.repo, msg.branchName, msg.remote, msg.createNewCommit, msg.squash) });
						break;

					case 'pushBranch':
						send({ command: 'pushBranch', willUpdateBranchConfig: msg.willUpdateBranchConfig, errors: await dataSource.pushBranchToMultipleRemotes(msg.repo, msg.branchName, msg.remotes, msg.setUpstream, msg.mode) });
						break;

					case 'pushStash':
						send({ command: 'pushStash', error: await dataSource.pushStash(msg.repo, msg.message, msg.includeUntracked) });
						break;

					case 'pushTag':
						send({ command: 'pushTag', repo: msg.repo, tagName: msg.tagName, remotes: msg.remotes, commitHash: msg.commitHash, errors: await dataSource.pushTag(msg.repo, msg.tagName, msg.remotes, msg.commitHash, msg.skipRemoteCheck) });
						break;

					case 'rebase':
						send({ command: 'rebase', actionOn: msg.actionOn, interactive: msg.interactive, error: await dataSource.rebase(msg.repo, msg.obj, msg.actionOn, msg.ignoreDate, msg.interactive) });
						break;

					case 'renameBranch':
						send({ command: 'renameBranch', error: await dataSource.renameBranch(msg.repo, msg.oldName, msg.newName) });
						break;

					case 'rescanForRepos':
						// In standalone mode, rescan process.argv paths
						break;

					case 'resetFileToRevision':
						send({ command: 'resetFileToRevision', error: await dataSource.resetFileToRevision(msg.repo, msg.commitHash, msg.filePath) });
						break;

					case 'resetToCommit':
						send({ command: 'resetToCommit', error: await dataSource.resetToCommit(msg.repo, msg.commit, msg.resetMode) });
						break;

					case 'revertCommit':
						send({ command: 'revertCommit', error: await dataSource.revertCommit(msg.repo, msg.commitHash, msg.parentIndex) });
						break;

					case 'setGlobalViewState':
						send({ command: 'setGlobalViewState', error: stateManager.setGlobalViewState(msg.state) });
						break;

					case 'setRepoState':
						stateManager.setRepoState(msg.repo, msg.state);
						break;

					case 'setWorkspaceViewState':
						send({ command: 'setWorkspaceViewState', error: stateManager.setWorkspaceViewState(msg.state) });
						break;

					case 'startCodeReview':
						send({
							command: 'startCodeReview',
							commitHash: msg.commitHash,
							compareWithHash: msg.compareWithHash,
							...stateManager.startCodeReview(msg.repo, msg.id, msg.files, msg.lastViewedFile)
						});
						break;

					case 'updateCodeReview':
						send({ command: 'updateCodeReview', error: stateManager.updateCodeReview(msg.repo, msg.id, msg.remainingFiles, msg.lastViewedFile) });
						break;

					/* ── Archive ────────────────────────────────────────── */

					case 'createArchive':
						// Return a download URL instead of a save dialog
						send({
							command: 'createArchive',
							error: 'Use GET /api/archive?repo=...&ref=...&format=zip to download an archive.'
						});
						break;

					/* ── No-ops (handled client-side already) ───────────── */
					case 'copyToClipboard':
					case 'copyFilePath':
					case 'openExternalUrl':
					case 'showErrorMessage':
					case 'openExtensionSettings':
					case 'viewScm':
						break;
				}
			} catch (err: any) {
				console.error('[server] Error handling command "' + (msg as any).command + '":', err?.message ?? err);
			}

			watcher.unmute();
		});

		ws.on('close', () => { watcher.stop(); });
	});

	const PORT = parseInt(process.env['GIT_GRAPH_PORT'] ?? '3000', 10);
	server.on('error', (err: NodeJS.ErrnoException) => {
		if (err.code === 'EADDRINUSE') {
			console.error(`[Git Graph] Port ${PORT} is already in use.\n  Stop the existing server with: npm stop\n  Or use a different port:    GIT_GRAPH_PORT=3001 npm start`);
		} else {
			console.error('[Git Graph] Server error:', err.message);
		}
		process.exit(1);
	});
	server.listen(PORT, () => {
		console.log('[Git Graph] Server running at http://localhost:' + PORT);
		if (repoList.length > 0) {
			console.log('[Git Graph] Watching repos:');
			repoList.forEach((r) => console.log('  ' + r));
		} else {
			console.warn('[Git Graph] No git repositories found. Pass paths as arguments: node server.js /path/to/repo');
		}
	});
}

main().catch((err) => {
	console.error('[Git Graph] Fatal startup error:', err);
	process.exit(1);
});
