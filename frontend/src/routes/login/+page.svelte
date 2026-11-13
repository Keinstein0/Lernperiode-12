<script>
 import Icon from '#lib/components/Icon.svelte'; import {api} from '#lib/api';
 let signingIn=$state(false); let session=$state(null); let error=$state('');
 async function signIn() {signingIn=true;error='';try {session=await api.login();}catch(e){error=e.message;}finally{signingIn=false;} }
</script>
<div class="login-layout"><header class="simple-header"><a href="/" class="brand-link" aria-label="UniSK home"><img src="/brand/unisk-mark.svg" alt="" class="brand-mark" width="30" height="30"/><span>UniSK</span></a></header><main class="login-form"><h1>Sign in to UniSK</h1><div class="login-box">{#if session}<p>Signed in as <strong>{session.user.login}</strong></p><a class="section-link" href="/">Continue to projects →</a>{:else}<button type="button" onclick={signIn} disabled={signingIn}><Icon name="github"/>{signingIn?'Signing in…':'Continue with GitHub'}</button>{/if}</div>{#if error}<p class="error-note" role="alert">{error}</p>{/if}<a class="back-home" href="/"><Icon name="arrow-left"/>Projects</a></main></div>
