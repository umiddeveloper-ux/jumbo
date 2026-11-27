# A third copy of the database: off the server and off the owner's computer

**Today (2026-11-27):** copy 1 = the server (`/opt/littlepolyglot/backups`, newest 14, nightly 03:30), copy 2 = the owner's computer
(`D:\JumboBackups`, newest 60, pulled daily 04:30). Both are fine against a damaged database or a deleted file, but if the server's disk or
account is lost **and** the computer is off, broken or stolen on the same day, there is nothing left. A third copy in another place fixes that.

**The dump holds personal data**: names, phone numbers, password hashes and the **access tokens** of 470 accounts (a token is a working
login). It must be **encrypted before it leaves the server**, with a key that is not stored on the server only. Size: about 110 MB a day
(already compressed), so 14 days = 1.5 GB, 30 days = 3.3 GB.

Not in the dump (see RESTORE.md): the picture files, the topic videos and the app downloads in `/opt/littlepolyglot/media`. Pictures can be
rebuilt, videos exist on the owner's computer (`TopicVideos\`); they change rarely and can be copied once by hand.

## Options

| | A. Object storage (Backblaze B2 or Cloudflare R2) + rclone | B. Google Drive + rclone | C. Second small VPS (or a home server) + rsync over ssh |
|---|---|---|---|
| Cost | B2: first 10 GB free, then about 6 USD per TB per month, so 3 GB costs nothing. R2: 10 GB free, no download fee | 15 GB free per Google account (shared with the mailbox) | about 3-5 USD a month for the cheapest VPS with 20+ GB |
| What the owner creates | an account (a bank card is asked for by R2, B2 asks it later or for bigger use), one private bucket, one **application key limited to that bucket** | a Google account (a separate one for the project is better) and one OAuth client for rclone | a VPS at another provider, an ssh key for the backup user |
| Work to set up | about 30 minutes | about 30 minutes, plus a browser login once (the token can expire and then the copy silently stops: the script alerts) | about 1 hour (user, ssh key, cron, disk) |
| Reliability | very good, made for this; versioning / object lock possible, so even a hacked server cannot delete old copies | good, but consumer service: quota, tokens, account lock risks | as good as you keep that server updated |
| Main risk | payment from Uzbekistan may be refused by the provider: check before choosing | a Google account tied to a person | one more machine to look after |

**Recommendation: A** (Backblaze B2 if a card is a problem, otherwise either), with **rclone crypt** (rclone encrypts the file names and the
contents itself, no extra tool). The key (two passwords) goes into the owner's password manager **and** on paper: **without it the copies are
useless and nobody can help**.

### What the owner must do for A (once)
1. Create the account and a **private** bucket, for example `jumbo-backups-<random>`; turn on "keep all versions" or object lock for 14 days.
2. Create an application key limited to that bucket (read, write, list; no delete if the provider allows). Send the key id and key to nobody:
   they are entered on the server by the owner, or given to Claude in a way the owner approves.
3. On the server: `sudo apt install rclone` (or the rclone install script), then `rclone config`: remote `b2` (type b2 or s3), then remote
   `jumbo-offsite` of type `crypt` over `b2:jumbo-backups-xxxx`, file names encrypted, two generated passwords. Print them, save them
   (`rclone config show` shows the obscured form; keep the plain passwords from the setup step).
4. Test by hand with `deploy/ops/offsite-sync.sh --dry-run`, then for real, then restore a file back with `rclone copy` and run
   `tools/restore-backup-test.ps1 -File <that file>` to prove the whole chain.
5. Cron on the server (after the nightly backup, 03:30 takes seconds): `45 3 * * * /opt/littlepolyglot/deploy/ops/offsite-sync.sh >> /home/ubuntu/offsite.log 2>&1`.

## The script
`deploy/ops/offsite-sync.sh` is a sketch: **not installed, not enabled, no account is set up**. It copies the newest local copy to the crypt
remote, checks the size at the other end, keeps the newest 30, tells the owner in Telegram (alert.sh) when anything fails, and never prints
secrets. It needs `rclone` and a remote called `jumbo-offsite` (the name can be changed with `JUMBO_OFFSITE_REMOTE`).

## Other cheap protection that does not need an account
- Keep a copy on a **USB disk** at home once a month (copy `D:\JumboBackups` after `tools\pull-backup.ps1`); disconnect it afterwards.
- Ask the hosting provider whether the VM has **disk snapshots** (a monthly snapshot of the whole VM protects against a lost server too).
- Check that `Jumbo backup pull` really ran (a file of today in `D:\JumboBackups`); `restore-backup-test.ps1` prints the date of the copy it uses.
