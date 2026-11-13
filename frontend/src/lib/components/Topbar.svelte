<script>
  import Icon from './Icon.svelte';
</script>

<header class="topbar">
  <a href="/" class="brand-link" aria-label="UniSK home">
    <img src="/brand/unisk-mark.svg" alt="" class="brand-mark" width="34" height="34" />
    <span class="product-name">UniSK</span>
  </a>
  <span class="topbar-divider" aria-hidden="true"></span>
  <span class="topbar-label">Workspace</span>
  <div class="topbar-right">
    <a href="/login" class="account-link"><Icon name="user" size={18}/><span>Account</span></a>
  </div>
</header>
