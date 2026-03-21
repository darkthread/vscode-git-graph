/**
 * register.ts
 *
 * Must be the VERY FIRST file executed by the backend process.
 * It intercepts Node.js module resolution so that any subsequent
 * `require('vscode')` (from src/dataSource.ts, src/config.ts, etc.)
 * resolves to our lightweight mock instead of failing.
 *
 * Usage: ts-node -r ./register.ts ./server.ts
 *         or compile and: node -r ./register.js ./server.js
 */

import Module from 'module';
import * as path from 'path';

const vscodeMockPath = path.resolve(__dirname, 'vscode-mock.js');

const original: Function = (Module as any)._resolveFilename;
(Module as any)._resolveFilename = function (request: string, ...rest: any[]) {
	if (request === 'vscode') return vscodeMockPath;
	return original.call(this, request, ...rest);
};
