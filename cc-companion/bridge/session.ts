/**
 * SDK 会话管理 — AsyncStream + query + 响应处理
 */

import { join } from 'path';
import { homedir } from 'os';
import { buildSystemPrompt } from '../rimworld/context.js';
import type { CompanionConfig } from '../companion/config.js';
import {Options, SYSTEM_PROMPT_DYNAMIC_BOUNDARY} from "@anthropic-ai/claude-agent-sdk";

// ========== AsyncStream ==========

export class AsyncStream<T = any> {
  private queue: T[] = [];
  private readResolve: ((v: IteratorResult<T>) => void) | undefined;
  private isDone = false;
  private started = false;

  [Symbol.asyncIterator](): AsyncIterator<T> {
    if (this.started) throw new Error('Stream can only be iterated once');
    this.started = true;
    return this;
  }

  async next(): Promise<IteratorResult<T>> {
    if (this.queue.length > 0) return { done: false, value: this.queue.shift()! };
    if (this.isDone) return { done: true, value: undefined as any };
    return new Promise((resolve) => { this.readResolve = resolve; });
  }

  enqueue(value: T): void {
    if (this.readResolve) {
      const r = this.readResolve;
      this.readResolve = undefined;
      r({ done: false, value });
    } else {
      this.queue.push(value);
    }
  }

  done(): void {
    this.isDone = true;
    if (this.readResolve) {
      const r = this.readResolve;
      this.readResolve = undefined;
      r({ done: true, value: undefined as any });
    }
  }
}

// ========== SDK 会话 ==========

export function createSession(sdk: any, config: CompanionConfig, abortController?: AbortController) {
  const inputStream = new AsyncStream<any>();

  const options = {
    cwd: config.projectPath,
    model: config.modelName || undefined,
    abortController,
    permissionMode: 'bypassPermissions',
    allowDangerouslySkipPermissions: true,
    disallowedTools: ['Bash', 'FileWrite', 'FileEdit', 'Read', 'Glob', 'Grep', 'NotebookEdit', 'WebFetch', 'EnterWorktree', 'ExitWorktree'],
    includePartialMessages: true,
    settingSources: config.settingSources,
    systemPrompt: [buildSystemPrompt(), SYSTEM_PROMPT_DYNAMIC_BOUNDARY],
    stderr: (data: string | Buffer) => {
      const text = typeof data === 'string' ? data : data.toString();
      process.stderr.write(`[sdk] ${text}`);
    },
  } as Options;

  console.log(`[cc-companion] 项目目录: ${config.projectPath}`);
  console.log(`[cc-companion] 会话将存储在: ${join(homedir(), '.claude', 'projects')}`);

  const queryIterator = sdk.query({ prompt: inputStream, options });
  console.log('[cc-companion] SDK 会话已创建');

  return { inputStream, queryIterator };
}

// ========== 后台响应处理 ==========

function sanitizePath(p: string): string {
  return p.replace(/[^a-zA-Z0-9]/g, '-').replace(/-+/g, '-');
}

export function createResponseProcessor(
  queryIterator: AsyncIterable<any>,
  cwd: string,
  onMessage?: (msg: any) => void,
  onInit?: (msg: any) => void,
) {
  let sessionId = 'pending';
  let processing = false;
  let initData: any = null;
  let currentModel = '';

  async function process(): Promise<void> {
    if (processing) { console.log('[cc-companion] processResponses 已在运行中，跳过'); return; }
    console.log('[cc-companion] processResponses 开始');
    processing = true;
    try {
      for await (const message of queryIterator) {
        const msgType: string = message?.type || 'unknown';

        if (msgType === 'system') {
          if (message.subtype === 'init') {
            initData = message;
            // 记录当前模型名
            if (message.model) currentModel = message.model;
            onInit?.(message);
            onMessage?.(message);
          }
          if (message.session_id && sessionId !== message.session_id) {
            sessionId = message.session_id;
            console.log(`[cc-companion] 会话 ID: ${sessionId}`);
            const sessionFile = join(homedir(), '.claude', 'projects',
              sanitizePath(cwd), `${sessionId}.jsonl`);
            console.log(`[cc-companion] 会话文件: ${sessionFile}`);
          }
        }

        if (msgType === 'assistant' || msgType === 'user' || msgType === 'stream_event') {
          onMessage?.(message);
          const content = message.message?.content;
          if (Array.isArray(content)) {
            for (const block of content) {
              if (block.type === 'text') {
                const text = block.text?.substring(0, 200) || '';
                console.log(`[assistant] ${text}${block.text?.length > 200 ? '...' : ''}`);
              } else if (block.type === 'tool_use') {
                console.log(`[tool_use] ${block.name}`);
              }
            }
          }
        }

        if (msgType === 'result') {
          // 附加当前模型名
          if (currentModel) (message as any).model = currentModel;
          const usage = message.usage;
          const durationMs = message.duration_ms;
          if (usage) {
            const inputTokens = usage.input_tokens ?? 0;
            const outputTokens = usage.output_tokens ?? 0;
            const cacheRead = usage.cache_read_input_tokens ?? 0;
            const cacheCreate = usage.cache_creation_input_tokens ?? 0;
            const totalTokens = inputTokens + outputTokens;
            const totalInput = inputTokens + cacheCreate + cacheRead;
            const cacheHitRate = totalInput > 0 ? (cacheRead / totalInput * 100).toFixed(0) : '0';
            const durationSec = durationMs ? (durationMs / 1000).toFixed(1) : '?';
            const fmt = (v: number) => v >= 1e6 ? (v/1e6).toFixed(1)+'M' : v >= 1e3 ? (v/1e3).toFixed(0)+'K' : String(v);
            console.log(`[result] 耗时${durationSec}s | Token ${fmt(totalTokens)} | 缓存 ${fmt(cacheRead)}(${cacheHitRate}%) | 输出 ${fmt(outputTokens)}`);
            (message as any)._usageText = [
              `耗时 ${durationSec}s | Token 合计 ${fmt(totalTokens)}`,
              `缓存命中 ${fmt(cacheRead)} (${cacheHitRate}%) | 输出 ${fmt(outputTokens)} | 新建 ${fmt(cacheCreate)}`,
            ].join('\n');
          } else {
            const summary = message.subtype === 'success'
              ? '执行成功' : `执行失败: ${message.errors?.join(', ') || 'unknown'}`;
            console.log(`[result] ${summary}`);
          }
          onMessage?.(message);
        }
      }
    } catch (err: any) {
      console.error(`[cc-companion] SDK 响应处理错误: ${err.message}`);
    }
    processing = false;
    console.log('[cc-companion] processResponses 结束');
  }

  return { process };
}
