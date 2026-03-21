/**
 * vscode-mock.ts
 *
 * Minimal VS Code API mock that lets src/dataSource.ts and src/config.ts run
 * in a plain Node.js process without the VS Code extension host.
 *
 * Only the subset actually exercised at runtime is implemented.
 */

import * as nodePath from 'path';
import * as fs from 'fs';
import * as os from 'os';

/* ------------------------------------------------------------------ */
/*  Runtime configuration (read from ~/.git-graph/settings.json)       */
/* ------------------------------------------------------------------ */

const SETTINGS_FILE = nodePath.join(os.homedir(), '.git-graph', 'settings.json');

function readSettings(): Record<string, any> {
	try { return JSON.parse(fs.readFileSync(SETTINGS_FILE, 'utf8')); }
	catch { return {}; }
}

let settings = readSettings();

// Git Graph extension defaults that mirror VSCode extension setting defaults.
const DEFAULTS: Record<string, any> = {
	'commitDetailsView.autoCenter': true,
	'commitDetailsView.fileViewType': 'File Tree',
	'commitDetailsView.location': 'Inline',
	'commitOrdering': 'date',
	'contextMenuActionsVisibility': {},
	'customBranchGlobPatterns': [],
	'customEmojiShortcodeMappings': [],
	'customPullRequestProviders': [],
	'date.format': 'Date & Time',
	'date.type': 'Author Date',
	'dateFormat': 'Date & Time',
	'dateType': 'Author Date',
	'defaultColumnVisibility': {},
	'dialog.addTag.pushToRemote': false,
	'dialog.addTag.type': 'Annotated',
	'dialog.applyStash.reinstateIndex': false,
	'dialog.cherryPick.noCommit': false,
	'dialog.cherryPick.recordOrigin': false,
	'dialog.createBranch.checkOut': false,
	'dialog.deleteBranch.forceDelete': false,
	'dialog.fetchIntoLocalBranch.forceFetch': false,
	'dialog.fetchRemote.prune': false,
	'dialog.fetchRemote.pruneTags': false,
	'dialog.general.referenceInputSpaceSubstitution': 'None',
	'dialog.merge.noCommit': false,
	'dialog.merge.rebase': false,
	'dialog.merge.squashCommits': false,
	'dialog.popStash.reinstateIndex': false,
	'dialog.pullBranch.noCommit': false,
	'dialog.pullBranch.rebase': false,
	'dialog.pullBranch.squashCommits': false,
	'dialog.rebase.ignoreDate': true,
	'dialog.rebase.launchInteractiveRebase': false,
	'dialog.resetCurrentBranchToCommit.mode': 'Mixed',
	'dialog.resetUncommittedChanges.mode': 'Mixed',
	'dialog.stashUncommittedChanges.includeUntracked': true,
	'enhancedAccessibility': false,
	'fetchAndPrune': false,
	'fetchAndPruneTags': false,
	'fetchAvatars': false,
	'fileEncoding': 'utf8',
	'graph.colours': [],
	'graph.style': 'rounded',
	'graph.uncommittedChanges': 'Open Circle at the Uncommitted Changes',
	'includeCommitsMentionedByReflogs': false,
	'initialLoadCommits': 300,
	'integratedTerminalShell': '',
	'keyboardShortcut.find': 'f',
	'keyboardShortcut.refresh': 'r',
	'keyboardShortcut.scrollToHead': 'h',
	'keyboardShortcut.scrollToStash': 's',
	'loadMoreCommits': 75,
	'loadMoreCommitsAutomatically': true,
	'markdown': true,
	'maxDepthOfRepoSearch': 0,
	'muteCommits.commits': false,
	'muteCommits.mergeCommits': true,
	'onlyFollowFirstParent': false,
	'onRepoLoad.scrollToHead': false,
	'onRepoLoad.showCheckedOutBranch': false,
	'openNewTabEditorGroup': 'Active',
	'referenceLabels.alignment': 'Normal',
	'referenceLabels.branchLabelsAlignedToGraph': false,
	'referenceLabels.combineLocalAndRemoteBranchLabels': true,
	'referenceLabels.tagLabelsOnRight': false,
	'repoDropdownOrder': 'Full Path',
	'repository.commits.fetchAvatars': false,
	'repository.commits.initialLoad': 300,
	'repository.commits.loadMore': 75,
	'repository.commits.loadMoreAutomatically': true,
	'repository.commits.mute.mergeCommits': true,
	'repository.commits.mute.commitsThatAreNotAncestorsOfHead': false,
	'repository.commits.onlyFollowFirstParent': false,
	'repository.commits.order': 'date',
	'repository.commits.showSignatureStatus': false,
	'repository.commits.useMailmap': false,
	'repository.fileEncoding': 'utf8',
	'repository.includeCommitsMentionedByReflogs': false,
	'repository.onLoad.scrollToHead': false,
	'repository.onLoad.showCheckedOutBranch': false,
	'repository.onLoad.showSpecificBranches': [],
	'repository.showRemoteBranchesV2': 'Default',
	'repository.showStashes': 'Default',
	'repository.showTags': 'Default',
	'repository.sign.commits': false,
	'repository.sign.tags': false,
	'showRemoteBranches': true,
	'showSignatureStatus': false,
	'showStashes': true,
	'showTags': true,
	'signCommits': false,
	'signTags': false,
	'useMailmap': false
};

function getValue(fullKey: string, defaultValue?: any): any {
	// Check user overrides first, then defaults
	const fromSettings = settings[fullKey];
	if (fromSettings !== undefined) return fromSettings;
	const fromDefaults = DEFAULTS[fullKey];
	if (fromDefaults !== undefined) return fromDefaults;
	return defaultValue;
}

/* ------------------------------------------------------------------ */
/*  VS Code API stubs                                                   */
/* ------------------------------------------------------------------ */

export const Uri = {
	file: (fsPath: string): any => ({
		fsPath: nodePath.normalize(fsPath),
		path: nodePath.normalize(fsPath).replace(/\\/g, '/'),
		scheme: 'file',
		toString: () => 'file://' + nodePath.normalize(fsPath).replace(/\\/g, '/'),
		with: (change: any) => ({ ...Uri.file(fsPath), ...change })
	}),
	parse: (uri: string): any => ({
		toString: () => uri,
		fsPath: uri.replace(/^file:\/\//, '')
	})
};

export const workspace = {
	getConfiguration: (section?: string, _scope?: any): any => {
		const prefix = section ? section + '.' : '';
		return {
			get: (key: string, defaultValue?: any) => {
				return getValue(prefix + key, getValue(key, defaultValue));
			},
			has: (key: string) => {
				const fullKey = prefix + key;
				return fullKey in settings || fullKey in DEFAULTS;
			},
			inspect: () => undefined,
			update: () => Promise.resolve()
		};
	},
	// Stub only — the backend uses its own fs.watch based watcher instead
	createFileSystemWatcher: (_pattern: string): any => ({
		onDidCreate: () => ({ dispose: () => {} }),
		onDidChange: () => ({ dispose: () => {} }),
		onDidDelete: () => ({ dispose: () => {} }),
		dispose: () => {}
	})
};

export const env = {
	clipboard: {
		writeText: (text: string): Promise<void> => Promise.resolve()
	},
	openExternal: (_uri: any): Promise<boolean> => Promise.resolve(true)
};

export const window = {
	showErrorMessage: (message: string) => {
		console.error('[VS Code Mock] showErrorMessage:', message);
		return Promise.resolve(undefined);
	},
	createOutputChannel: (name: string): any => ({
		name,
		appendLine: (value: string) => { if (process.env['GIT_GRAPH_VERBOSE']) console.log('[' + name + ']', value); },
		append: (value: string) => { if (process.env['GIT_GRAPH_VERBOSE']) process.stdout.write('[' + name + '] ' + value); },
		show: () => {},
		hide: () => {},
		dispose: () => {}
	}),
	createTerminal: (_options: any): any => ({
		name: 'mock-terminal',
		sendText: () => {},
		show: () => {}
	}),
	showSaveDialog: (_options: any): Promise<any | undefined> => {
		return Promise.resolve(undefined);
	},
	showTextDocument: (_doc: any, _options: any): Promise<any> => Promise.resolve({}),
	showInputBox: (_options: any): Promise<string | undefined> => Promise.resolve(undefined),
	activeTextEditor: undefined
};

export const commands = {
	executeCommand: (_command: string, ..._args: any[]): Promise<any> => Promise.resolve(undefined)
};

export const extensions = {
	getExtension: (_id: string): any => undefined
};

export const ViewColumn = {
	Active: -1, Beside: -2, One: 1, Two: 2, Three: 3, Four: 4, Five: 5
};

export const ThemeColor = class ThemeColor {
	constructor(public id: string) {}
};

// Types only — no runtime values needed
export type ExtensionContext = any;
export type Memento = any;
export type ConfigurationChangeEvent = { affectsConfiguration(section: string): boolean };
export type TextDocument = any;
export type TextEditor = any;
export type OutputChannel = any;
