/**
 * stateManager.ts
 *
 * Replaces VS Code's ExtensionContext globalState / workspaceState with plain
 * JSON files stored in ~/.git-graph/.
 *
 * Mirrors the public API of src/extensionState.ts that is called by the server.
 */

import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import {
	BooleanOverride,
	CodeReview,
	FileViewType,
	GitGraphViewGlobalState,
	GitGraphViewWorkspaceState,
	GitRepoSet,
	GitRepoState,
	RepoCommitOrdering
} from '../src/types';

/* ------------------------------------------------------------------ */
/*  Default values (mirrored from src/extensionState.ts)               */
/* ------------------------------------------------------------------ */

export const DEFAULT_REPO_STATE: GitRepoState = {
	cdvDivider: 0.5,
	cdvHeight: 250,
	columnWidths: null,
	commitOrdering: RepoCommitOrdering.Default,
	fileViewType: FileViewType.Default,
	hideRemotes: [],
	includeCommitsMentionedByReflogs: BooleanOverride.Default,
	issueLinkingConfig: null,
	lastImportAt: 0,
	name: null,
	onlyFollowFirstParent: BooleanOverride.Default,
	onRepoLoadShowCheckedOutBranch: BooleanOverride.Default,
	onRepoLoadShowSpecificBranches: null,
	pullRequestConfig: null,
	showRemoteBranches: true,
	showRemoteBranchesV2: BooleanOverride.Default,
	showStashes: BooleanOverride.Default,
	showTags: BooleanOverride.Default,
	workspaceFolderIndex: null
};

const DEFAULT_GLOBAL_VIEW_STATE: GitGraphViewGlobalState = {
	alwaysAcceptCheckoutCommit: false,
	issueLinkingConfig: null,
	pushTagSkipRemoteCheck: false
};

const DEFAULT_WORKSPACE_VIEW_STATE: GitGraphViewWorkspaceState = {
	findIsCaseSensitive: false,
	findIsRegex: false,
	findOpenCommitDetailsView: false
};

/* ------------------------------------------------------------------ */
/*  Persistence helpers                                                  */
/* ------------------------------------------------------------------ */

export interface CodeReviewData {
	lastActive: number;
	lastViewedFile: string | null;
	remainingFiles: string[];
}
export type CodeReviews = { [repo: string]: { [id: string]: CodeReviewData } };

interface PersistedState {
	repos: GitRepoSet;
	lastActiveRepo: string | null;
	ignoredRepos: string[];
	globalViewState: GitGraphViewGlobalState;
	workspaceViewState: GitGraphViewWorkspaceState;
	codeReviews: CodeReviews;
}

const STORAGE_DIR = path.join(os.homedir(), '.git-graph');
const STATE_FILE   = path.join(STORAGE_DIR, 'state.json');

function readState(): PersistedState {
	try {
		return JSON.parse(fs.readFileSync(STATE_FILE, 'utf8'));
	} catch {
		return {
			repos: {},
			lastActiveRepo: null,
			ignoredRepos: [],
			globalViewState: { ...DEFAULT_GLOBAL_VIEW_STATE },
			workspaceViewState: { ...DEFAULT_WORKSPACE_VIEW_STATE },
			codeReviews: {}
		};
	}
}

function writeState(state: PersistedState): void {
	try {
		if (!fs.existsSync(STORAGE_DIR)) fs.mkdirSync(STORAGE_DIR, { recursive: true } as any);
		fs.writeFileSync(STATE_FILE, JSON.stringify(state, null, 2));
	} catch (err) {
		console.error('[StateManager] Failed to write state:', err);
	}
}

/* ------------------------------------------------------------------ */
/*  StateManager class                                                   */
/* ------------------------------------------------------------------ */

export class StateManager {
	private state: PersistedState;

	constructor() {
		this.state = readState();
	}

	private save() {
		writeState(this.state);
	}

	/* Repos */

	getRepos(): GitRepoSet {
		const output: GitRepoSet = {};
		for (const repo of Object.keys(this.state.repos)) {
			output[repo] = Object.assign({}, DEFAULT_REPO_STATE, this.state.repos[repo]);
		}
		return output;
	}

	saveRepos(repos: GitRepoSet): void {
		this.state.repos = repos;
		this.save();
	}

	setRepoState(repo: string, repoState: GitRepoState): void {
		this.state.repos[repo] = Object.assign({}, DEFAULT_REPO_STATE, repoState);
		this.save();
	}

	/* Last active repo */

	getLastActiveRepo(): string | null {
		return this.state.lastActiveRepo ?? null;
	}

	setLastActiveRepo(repo: string | null): void {
		this.state.lastActiveRepo = repo;
		this.save();
	}

	/* Global & workspace view state */

	getGlobalViewState(): GitGraphViewGlobalState {
		return Object.assign({}, DEFAULT_GLOBAL_VIEW_STATE, this.state.globalViewState);
	}

	setGlobalViewState(state: GitGraphViewGlobalState): null {
		this.state.globalViewState = state;
		this.save();
		return null;
	}

	getWorkspaceViewState(): GitGraphViewWorkspaceState {
		return Object.assign({}, DEFAULT_WORKSPACE_VIEW_STATE, this.state.workspaceViewState);
	}

	setWorkspaceViewState(state: GitGraphViewWorkspaceState): null {
		this.state.workspaceViewState = state;
		this.save();
		return null;
	}

	/* Code reviews */

	getCodeReview(repo: string, id: string): CodeReview | null {
		const reviews = this.state.codeReviews;
		if (reviews[repo] && reviews[repo][id]) {
			const r = reviews[repo][id];
			return { id, lastActive: r.lastActive, lastViewedFile: r.lastViewedFile, remainingFiles: r.remainingFiles };
		}
		return null;
	}

	startCodeReview(repo: string, id: string, files: string[], lastViewedFile: string | null): { codeReview: CodeReview | null; error: null } {
		if (!this.state.codeReviews[repo]) this.state.codeReviews[repo] = {};
		const now = Date.now();
		this.state.codeReviews[repo][id] = { lastActive: now, lastViewedFile, remainingFiles: files.slice() };
		this.save();
		const codeReview: CodeReview = { id, lastActive: now, lastViewedFile, remainingFiles: files.slice() };
		return { codeReview, error: null };
	}

	endCodeReview(repo: string, id: string): void {
		if (this.state.codeReviews[repo]) {
			delete this.state.codeReviews[repo][id];
			if (Object.keys(this.state.codeReviews[repo]).length === 0) {
				delete this.state.codeReviews[repo];
			}
			this.save();
		}
	}

	updateCodeReview(repo: string, id: string, remainingFiles: string[], lastViewedFile: string | null): null {
		if (this.state.codeReviews[repo] && this.state.codeReviews[repo][id]) {
			this.state.codeReviews[repo][id].remainingFiles = remainingFiles;
			this.state.codeReviews[repo][id].lastViewedFile = lastViewedFile;
			this.state.codeReviews[repo][id].lastActive = Date.now();
			this.save();
		}
		return null;
	}

	/* Avatar storage path (kept for DataSource compatibility) */

	getAvatarStoragePath(): string {
		const dir = path.join(STORAGE_DIR, 'avatars');
		if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true } as any);
		return dir;
	}
}
