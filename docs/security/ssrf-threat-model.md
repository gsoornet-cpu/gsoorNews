# SSRF threat model — source ingestion fetcher

## What can reach an attacker-influenced URL

`Source.FeedUrl` is operator-configured today (seeded/edited by an admin,
not directly by end users), so the immediate risk is lower than a
user-submitted URL. It's still treated as untrusted for three reasons:
(1) a compromised or careless admin account is a realistic threat, not a
theoretical one; (2) Phase 2/3 plans include editorial workflows that may
let more roles add sources; (3) even a "trusted" source's feed can contain
redirects pointing anywhere, and RSS `<link>` values are attacker-controlled
content from the feed publisher, not from us. So the guard treats every URL
segment — the original FeedUrl AND every redirect hop — identically.

## Attack scenarios considered

1. **Direct internal target**: `FeedUrl` set (accidentally or maliciously)
   to `http://localhost:5432`, `http://10.0.0.5/admin`, or similar —
   blocked by `PrivateNetworkGuard` rejecting the resolved IP before
   connecting.
2. **Cloud metadata endpoint**: `http://169.254.169.254/latest/meta-data/`
   (AWS/Azure/GCP instance metadata, a classic SSRF-to-credential-theft
   path) — blocked as part of the `169.254.0.0/16` link-local range.
3. **DNS rebinding**: an attacker-controlled domain resolves to a public IP
   the first time (passes validation) and to `127.0.0.1` or an internal IP
   the second time (at actual connection time) — defeated by resolving
   **once** and connecting directly to the validated IP via
   `SocketsHttpHandler.ConnectCallback`, never letting the runtime
   re-resolve the hostname at connect time.
4. **Redirect to an internal target**: the feed URL itself is public and
   safe, but its HTTP response redirects (3xx) to an internal address —
   `AllowAutoRedirect` is disabled; redirects are followed manually, and
   each new URL goes through the same validation + pinned-connect path as
   the original.
5. **IPv4-mapped-IPv6 bypass**: `::ffff:169.254.169.254` used to smuggle a
   blocked IPv4 address past an IPv6-only check — `PrivateNetworkGuard`
   unwraps mapped addresses before classifying them.
6. **Non-HTTP(S) scheme abuse**: `file://`, `gopher://`, etc. used to reach
   local files or speak arbitrary internal protocols — rejected before any
   DNS resolution is attempted (`TryValidateUrlShape`).
7. **Resource exhaustion via oversized/slow response**: a malicious or
   misbehaving source returning gigabytes of data or trickling bytes
   forever — bounded by `IngestionOptions.MaxResponseBytes` (response is
   read incrementally and aborted once the cap is exceeded, never buffered
   unbounded) and a combined connect+read timeout.

## What is explicitly NOT covered by this slice

- **XXE (XML External Entity) in feed content** is a related but distinct
  attack surface, handled separately in `RssFeedParser` via
  `XmlReaderSettings.DtdProcessing = Prohibit` and `XmlResolver = null` —
  see that file's tests for the XXE-rejection proof.
- **IPv6 zone IDs / scoped addresses** and **decimal/octal/hex IP
  obfuscation in the hostname string** (e.g. `http://2130706433/` as an
  integer form of `127.0.0.1`) are not separately special-cased — .NET's
  `Uri`/`Dns` parsing normalizes most such forms before `PrivateNetworkGuard`
  ever sees them, but this has not been exhaustively fuzzed against every
  known obfuscation technique. Flagging this honestly as a residual risk
  worth a dedicated fuzzing pass before this fetcher is exposed to
  less-trusted input (e.g. a future "suggest a source" user-facing feature).
- **Per-source or global fetch rate limiting** is not implemented in this
  slice — a compromised/malicious source responding slowly is bounded by
  the timeout, but nothing yet prevents a scheduler from hammering a source
  too frequently. That belongs with the scheduler (Hangfire), a later
  slice.
