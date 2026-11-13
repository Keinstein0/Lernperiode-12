<script lang="ts">
  let { source }: { source: string } = $props();
  type Token = { text: string; kind: 'text'|'strong'|'code'|'link'; href?: string };
  type Block = { kind: 'paragraph'|'code'|'list'|'quote'|'heading'; text: string; language?: string };
  function tokens(line: string): Token[] {
    const out: Token[] = [];
    const pattern = /(`[^`\n]+`|\*\*[^*\n]+\*\*|\[[^\]\n]+\]\(https?:\/\/[^\s)]+\))/g;
    let last = 0;
    for (const match of line.matchAll(pattern)) {
      if (match.index > last) out.push({kind:'text',text:line.slice(last,match.index)});
      const s=match[0];
      if(s.startsWith('`')) out.push({kind:'code',text:s.slice(1,-1)});
      else if(s.startsWith('**')) out.push({kind:'strong',text:s.slice(2,-2)});
      else { const m=s.match(/^\[([^\]]+)\]\((https?:\/\/[^\s)]+)\)$/);if(m)out.push({kind:'link',text:m[1],href:m[2]}); }
      last=match.index+s.length;
    }
    if(last<line.length)out.push({kind:'text',text:line.slice(last)});
    return out;
  }
  function parse(input: string): Block[] {
    const result: Block[]=[];let code=false;let buffer:string[]=[];let language='';
    for(const line of input.split('\n')){
      if(line.startsWith('```')) {if(code){result.push({kind:'code',text:buffer.join('\n'),language});buffer=[];code=false;}else{code=true;language=line.slice(3).trim().slice(0,30);}continue;}
      if(code){buffer.push(line);continue;}
      if(/^#{1,3} /.test(line))result.push({kind:'heading',text:line.replace(/^#{1,3} /,'')});
      else if(/^[-*] /.test(line))result.push({kind:'list',text:line.slice(2)});
      else if(/^> /.test(line))result.push({kind:'quote',text:line.slice(2)});
      else result.push({kind:'paragraph',text:line});
    }
    if(code)result.push({kind:'code',text:buffer.join('\n'),language});
    return result;
  }
  let blocks=$derived(parse(source));
</script>
{#snippet inlineText(line:string)}
  {#each tokens(line) as token}
    {#if token.kind==='strong'}<strong>{token.text}</strong>
    {:else if token.kind==='code'}<code>{token.text}</code>
    {:else if token.kind==='link'}<a href={token.href} target="_blank" rel="noopener noreferrer">{token.text}</a>
    {:else}{token.text}{/if}
  {/each}
{/snippet}
<div class="chat-markdown">
{#each blocks as block}
  {#if block.kind==='code'}<div class="code-block"><div class="code-heading"><span>{block.language||'Code'}</span><button type="button" onclick={()=>void navigator.clipboard?.writeText(block.text)}>Copy</button></div><pre><code>{block.text}</code></pre></div>
  {:else if block.kind==='heading'}<h3>{@render inlineText(block.text)}</h3>
  {:else if block.kind==='list'}<div class="markdown-list">• {@render inlineText(block.text)}</div>
  {:else if block.kind==='quote'}<blockquote>{@render inlineText(block.text)}</blockquote>
  {:else}<p>{@render inlineText(block.text||'\u00a0')}</p>{/if}
{/each}
</div>
