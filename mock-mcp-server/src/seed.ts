import type { RegisteredNode, TaskRecord } from './types.js';

/** Fake nodes so the client has something to discover right after startup. */
export function seedNodes(): RegisteredNode[] {
  const now = new Date().toISOString();
  return [
    {
      nodeId: 'node-claude-code-1',
      status: 'free',
      lastSeen: now,
      agentCard: {
        name: 'Claude Code Worker',
        description: 'Implements features and fixes bugs in a git repository using Claude Code.',
        url: 'http://claude-code-1.local:5556',
        version: '1.0.0',
        capabilities: { streaming: true, pushNotifications: false, extendedAgentCard: true },
        skills: [
          {
            id: 'implement-feature',
            name: 'Implement feature',
            description: 'Writes code for a described feature and pushes it to its own branch.',
            tags: ['code', 'typescript', 'python', 'git'],
          },
          {
            id: 'fix-bug',
            name: 'Fix bug',
            description: 'Reproduces and fixes a bug, adds a regression test.',
            tags: ['code', 'debugging', 'tests'],
          },
        ],
        workspaceModes: ['git', 'conngit'],
      },
      extendedCard: { model: 'claude-opus-5-5', maxConcurrentTasks: 1 },
    },
    {
      nodeId: 'node-research-1',
      status: 'busy',
      lastSeen: now,
      agentCard: {
        name: 'Research Agent',
        description: 'Searches the web and writes summarised research reports as markdown.',
        url: 'http://research-1.local:5557',
        version: '0.3.1',
        capabilities: { streaming: true, pushNotifications: false, extendedAgentCard: false },
        skills: [
          {
            id: 'research-report',
            name: 'Research report',
            description: 'Researches a topic and returns a markdown report with sources.',
            tags: ['research', 'web', 'markdown', 'summary'],
          },
        ],
        workspaceModes: ['files'],
      },
    },
    {
      nodeId: 'node-docs-1',
      status: 'free',
      lastSeen: now,
      agentCard: {
        name: 'Docs Writer',
        description: 'Writes and updates README files and API documentation.',
        url: 'http://docs-1.local:5558',
        version: '0.1.0',
        capabilities: { streaming: false, pushNotifications: false, extendedAgentCard: false },
        skills: [
          {
            id: 'write-docs',
            name: 'Write docs',
            description: 'Creates or updates documentation for a repository.',
            tags: ['docs', 'markdown', 'readme', 'git'],
          },
        ],
        workspaceModes: ['git', 'files'],
      },
    },
    {
      nodeId: 'node-roblox-1',
      status: 'offline',
      lastSeen: new Date(Date.now() - 1000 * 60 * 45).toISOString(),
      agentCard: {
        name: 'Roblox Builder',
        description: 'Builds Roblox places and Luau scripts.',
        url: 'http://roblox-1.local:5559',
        version: '0.0.2',
        capabilities: { streaming: false, pushNotifications: false, extendedAgentCard: false },
        skills: [
          {
            id: 'luau-script',
            name: 'Luau script',
            description: 'Writes Luau game scripts for Roblox.',
            tags: ['roblox', 'luau', 'game'],
          },
        ],
        workspaceModes: ['files'],
      },
    },
  ];
}

/** A few tasks in different states, so the UI can render every state from the start. */
export function seedTasks(): TaskRecord[] {
  const t = (minutesAgo: number) => new Date(Date.now() - minutesAgo * 60_000).toISOString();
  return [
    {
      nodeId: 'node-claude-code-1',
      createdAt: t(120),
      task: {
        id: 'task-0001',
        contextId: 'ctx-login-page',
        status: { state: 'TASK_STATE_COMPLETED', timestamp: t(100) },
        history: [
          {
            messageId: 'msg-0001',
            role: 'user',
            parts: [{ text: 'Build a login page with GitHub OAuth in the Svelte frontend.' }],
            taskId: 'task-0001',
            contextId: 'ctx-login-page',
          },
          {
            messageId: 'msg-0002',
            role: 'agent',
            parts: [{ text: 'Done. Pushed to branch node/claude-code-1/login-page.' }],
            taskId: 'task-0001',
            contextId: 'ctx-login-page',
          },
        ],
        artifacts: [
          {
            artifactId: 'art-0001',
            name: 'result',
            parts: [
              {
                data: {
                  gitRepoUrl: 'https://github.com/Keinstein0/Lernperiode-12',
                  gitBranch: 'node/claude-code-1/login-page',
                  filesOfInterest: ['frontend/src/routes/login/+page.svelte'],
                },
              },
            ],
          },
        ],
      },
    },
    {
      nodeId: 'node-research-1',
      createdAt: t(5),
      task: {
        id: 'task-0002',
        contextId: 'ctx-a2a-research',
        status: { state: 'TASK_STATE_WORKING', timestamp: t(4) },
        history: [
          {
            messageId: 'msg-0003',
            role: 'user',
            parts: [{ text: 'Compare existing A2A harnesses and summarise their trade-offs.' }],
            taskId: 'task-0002',
            contextId: 'ctx-a2a-research',
          },
        ],
        artifacts: [],
      },
    },
    {
      nodeId: 'node-docs-1',
      createdAt: t(30),
      task: {
        id: 'task-0003',
        contextId: 'ctx-readme',
        status: {
          state: 'TASK_STATE_INPUT_REQUIRED',
          timestamp: t(25),
          message: {
            messageId: 'msg-0005',
            role: 'agent',
            parts: [{ text: 'Should the README be written in German or English?' }],
            taskId: 'task-0003',
            contextId: 'ctx-readme',
          },
        },
        history: [
          {
            messageId: 'msg-0004',
            role: 'user',
            parts: [{ text: 'Rewrite the root README for UniSKv2.' }],
            taskId: 'task-0003',
            contextId: 'ctx-readme',
          },
          {
            messageId: 'msg-0005',
            role: 'agent',
            parts: [{ text: 'Should the README be written in German or English?' }],
            taskId: 'task-0003',
            contextId: 'ctx-readme',
          },
        ],
        artifacts: [],
      },
    },
  ];
}
