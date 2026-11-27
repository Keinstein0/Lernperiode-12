<script lang="ts">
	import { onMount } from 'svelte';
	import AppShell from '#lib/components/AppShell.svelte';
	import { mcp, type NodeSummary, type RoutedTask, type Part } from '#lib/api/mcp';

	// Dev page: talks to the router's MCP interface (or the mock in mock-mcp-server/).
	let query = $state('fix a typescript bug');
	let nodes = $state<NodeSummary[]>([]);
	let selectedNode = $state('');
	let message = $state('Add dark mode');
	let tasks = $state<RoutedTask[]>([]);
	let error = $state('');
	let busy = $state(false);

	const isFinal = (t: RoutedTask) =>
		!['TASK_STATE_SUBMITTED', 'TASK_STATE_WORKING'].includes(t.task.status.state);

	const partText = (parts: Part[]) =>
		parts.map((p) => ('text' in p ? p.text : JSON.stringify(p.data))).join(' ');

	async function run(fn: () => Promise<void>) {
		busy = true;
		error = '';
		try {
			await fn();
		} catch (e) {
			error = e instanceof Error ? e.message : String(e);
		} finally {
			busy = false;
		}
	}

	const searchNodes = () =>
		run(async () => {
			nodes = (await mcp.getNodes(query)).nodes;
			selectedNode ||= nodes.find((n) => n.status === 'free')?.nodeId ?? '';
		});

	const loadTasks = () =>
		run(async () => {
			tasks = (await mcp.getTasks()).tasks;
		});

	const send = () =>
		run(async () => {
			await mcp.sendTask(selectedNode, message);
			tasks = (await mcp.getTasks()).tasks;
			nodes = (await mcp.getNodes(query)).nodes; // node status changes to busy
		});

	const cancel = (taskId: string) =>
		run(async () => {
			await mcp.cancelTask(taskId);
			tasks = (await mcp.getTasks()).tasks;
		});

	const answer = (t: RoutedTask) => {
		const reply = prompt('Answer for the agent:');
		if (!reply) return;
		void run(async () => {
			await mcp.sendTask(t.nodeId, reply, { taskId: t.task.id });
			tasks = (await mcp.getTasks()).tasks;
		});
	};

	onMount(() => {
		void searchNodes();
		void loadTasks();
		// Poll while any task is still running (no streaming yet).
		const timer = setInterval(async () => {
			if (tasks.some((t) => !isFinal(t))) {
				try {
					tasks = (await mcp.getTasks()).tasks;
					nodes = (await mcp.getNodes(query)).nodes;
				} catch {
					/* shown on next manual action */
				}
			}
		}, 1000);
		return () => clearInterval(timer);
	});
</script>

<AppShell>
	<div class="content-wrap">
		<div class="breadcrumbs">Workspace <span>/</span> MCP test</div>
		<header class="page-heading">
			<div><h1>MCP test</h1></div>
		</header>

		{#if error}<p class="error-note" role="alert">{error}</p>{/if}

		<section class="agent-group">
			<header><h2>Find nodes (get_nodes)</h2></header>
			<form
				class="task-toolbar"
				onsubmit={(e) => {
					e.preventDefault();
					void searchNodes();
				}}
			>
				<input bind:value={query} placeholder="What should be done?" />
				<button class="secondary-button" disabled={busy}>Search</button>
			</form>
			<div class="agent-directory">
				{#each nodes as node (node.nodeId)}
					<label class="directory-row">
						<div>
							<strong class="agent-name">
								<input type="radio" bind:group={selectedNode} value={node.nodeId} />
								{node.name}
							</strong>
							<small>{node.nodeId} · score {node.score} · {node.skills.join(', ')}</small>
						</div>
						<span class="agent-status">{node.status}</span>
					</label>
				{/each}
			</div>
		</section>

		<section class="agent-group">
			<header><h2>Send task (send_task)</h2></header>
			<form
				class="task-toolbar"
				onsubmit={(e) => {
					e.preventDefault();
					void send();
				}}
			>
				<input bind:value={message} placeholder='Try "fail" or a "?"' />
				<button class="primary-button" disabled={busy || !selectedNode}>Send to {selectedNode || '…'}</button>
			</form>
		</section>

		<section class="agent-group">
			<header>
				<h2>Tasks (get_tasks)</h2>
				<span class="count">{tasks.length}</span>
			</header>
			<div class="agent-directory">
				{#each tasks as t (t.task.id)}
					<div class="directory-row">
						<div>
							<strong>{t.task.id}</strong>
							<small>
								{t.nodeId} · {t.task.status.state.replace('TASK_STATE_', '')}
								{#if t.task.status.message} · “{partText(t.task.status.message.parts)}”{/if}
							</small>
							{#each t.task.artifacts as a (a.artifactId)}
								<small>Result: {partText(a.parts)}</small>
							{/each}
						</div>
						<span class="row-end">
							{#if t.task.status.state === 'TASK_STATE_INPUT_REQUIRED'}
								<button class="text-button" onclick={() => answer(t)}>Answer</button>
							{/if}
							{#if !['TASK_STATE_COMPLETED', 'TASK_STATE_FAILED', 'TASK_STATE_CANCELED'].includes(t.task.status.state)}
								<button class="text-button" onclick={() => cancel(t.task.id)}>Cancel</button>
							{/if}
						</span>
					</div>
				{/each}
			</div>
		</section>
	</div>
</AppShell>
