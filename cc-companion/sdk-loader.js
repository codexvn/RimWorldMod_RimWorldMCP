/**
 * 加载 Claude Agent SDK — 按优先级搜索多个安装位置
 *
 * 1. CC GUI 依赖目录: ~/.codemoss/dependencies/claude-sdk/
 * 2. 全局 npm: %APPDATA%/npm/node_modules/ 或 /usr/local/lib/node_modules/
 * 3. 本地 node_modules (开发模式)
 */

import { existsSync, readFileSync } from 'fs';
import { join, dirname } from 'path';
import { pathToFileURL } from 'url';
import { homedir } from 'os';

const SDK_PACKAGE = '@anthropic-ai/claude-agent-sdk';

function getCandidateDirs() {
  const home = homedir();
  const candidates = [];

  // CC GUI 安装路径
  candidates.push(join(home, '.codemoss', 'dependencies', 'claude-sdk', 'node_modules', SDK_PACKAGE));

  // Windows 全局 npm
  if (process.env.APPDATA) {
    candidates.push(join(process.env.APPDATA, 'npm', 'node_modules', SDK_PACKAGE));
  }

  // Unix 全局 npm
  candidates.push(join('/usr/local/lib/node_modules', SDK_PACKAGE));
  candidates.push(join('/usr/lib/node_modules', SDK_PACKAGE));

  // 本地 (开发)
  candidates.push(join(process.cwd(), 'node_modules', SDK_PACKAGE));

  return candidates;
}

function resolveEntryFile(packageDir) {
  const pkgJsonPath = join(packageDir, 'package.json');
  let candidate = null;

  if (existsSync(pkgJsonPath)) {
    try {
      const pkg = JSON.parse(readFileSync(pkgJsonPath, 'utf8'));
      const exports = pkg.exports;
      const root = exports?.['.'] ?? exports;
      if (typeof root === 'string') candidate = root;
      else if (root && typeof root === 'object') {
        candidate = root.import ?? root.default ?? root.module;
      }
      if (!candidate) {
        candidate = pkg.module ?? pkg.main;
      }
    } catch {}
  }

  if (candidate) return join(packageDir, candidate);

  // 启发式
  for (const name of ['sdk.mjs', 'index.mjs', 'index.js', 'dist/index.js', 'dist/index.mjs']) {
    const p = join(packageDir, name);
    if (existsSync(p)) return p;
  }
  return null;
}

export async function loadClaudeSdk() {
  for (const pkgDir of getCandidateDirs()) {
    if (!existsSync(pkgDir)) continue;
    const entry = resolveEntryFile(pkgDir);
    if (!entry) continue;

    console.log(`[cc-companion] 找到 SDK: ${entry}`);
    try {
      const sdk = await import(pathToFileURL(entry).href);
      if (typeof sdk.query === 'function') {
        console.log(`[cc-companion] SDK 已加载 (exports: ${Object.keys(sdk).join(', ')})`);
        return sdk;
      }
      console.warn(`[cc-companion] SDK 缺少 query 函数，尝试下一个位置...`);
    } catch (err) {
      console.warn(`[cc-companion] SDK 加载失败 (${pkgDir}): ${err.message}`);
    }
  }

  const searched = getCandidateDirs().map(d => `  - ${d}`).join('\n');
  throw new Error(
    `找不到 Claude Agent SDK。请确保已安装 CC GUI 的 Claude SDK 依赖。\n` +
    `搜索路径:\n${searched}`
  );
}
