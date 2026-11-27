# Payme (Paycom) payment of a paid version, variant A: an order per version

State: **built and tested locally, NOT on the server, NOT switched on.** Without the keys below the Merchant API answers `-32504` to everybody
and the site hides the button, so deploying the code does not start any payment.

## How it works

1. The learner opens `/plans`, picks Silver or Gold and a month or a year, and presses **Pay by card**.
2. The site asks `POST api/payments/orders`; the API makes an **order** (`payment_orders`) with the price from `plan_prices` (never from the client)
   and returns the Payme page address. The learner is sent to `https://checkout.paycom.uz/...` (or `https://test.paycom.uz/...` in the sandbox).
3. Payme calls our **Merchant API** `POST https://jumbo.uz/api/payme` (JSON-RPC, Basic authorization):
   `CheckPerformTransaction` (can this order be paid; answers with the fiscal receipt data) → `CreateTransaction` (reserves the order)
   → the learner enters the card → `PerformTransaction` (the version is switched on, in one database transaction).
4. Payme sends the learner back to `https://jumbo.uz/plans/paid/{order}`; that page asks the order every 3 seconds (about a minute) until it is paid.
5. A refund in the Payme cabinet calls `CancelTransaction`; the period that the payment gave is taken back. A payment that nobody finishes is cancelled
   after 12 hours (reason 4) by a job that runs every minute; the order can then be paid again.

Rules worth knowing: an order has at most one live transaction (a database index guarantees it); a repeated call of Payme gets the same answer;
a higher version in force makes a lower one impossible (the same rules as buying from the balance); an account that is deleted leaves its orders
and transactions for the accounts of the business (without the learner), and a payment that was not finished for such an order is refused.

## What to tell Payme (when they open the cash register)

| | |
|---|---|
| Name of the cash register | `Jumbo.uz` |
| Endpoint URL | `https://jumbo.uz/api/payme` |
| Account parameter | one field, `order_id` (the number of the order) |
| Account type | one-time (an order is paid once) |

Payme then gives: the **cash register id** (`Payme__MerchantId`), the **login** of the Basic authorization (`Payme__Login`; the docs do not say
which value, Payme tells it), the **key** (`Payme__Key`, 36 characters; the sandbox has its own `TEST_KEY`).

## Settings (environment variables of the `api` container; NEVER in the repository)

| variable | what | example |
|---|---|---|
| `Payme__Login` | login of the Basic authorization | given by Payme |
| `Payme__Key` | the key (password) of the cash register; the **test key in the sandbox**, the real key in production | given by Payme |
| `Payme__MerchantId` | the cash register id; goes in the checkout link as `m` | `587f72c7...` |
| `Payme__CheckoutUrl` | the Payme page: sandbox `https://test.paycom.uz`, production `https://checkout.paycom.uz` (the default) | |
| `Payme__ReturnBaseUrl` | the site the learner returns to (default `https://jumbo.uz`) | |
| `Payme__Mxik` | the product code (MXIK / IKPU) of the service, from the accountant | check on `https://tasnif.soliq.uz/attribute/<code>` |
| `Payme__PackageCode` | the unit of measure tied to that MXIK code | |
| `Payme__VatPercent` | the VAT percent on the receipt (`0` if there is no VAT) | `12` |
| `Payme__CheckIp` | only Payme's addresses may call the API; default: on in production | `true` |
| `Payme__AllowedIps` | addresses and ranges, comma separated; default `185.234.113.1-185.234.113.15` | |

Without all three of `Mxik`, `PackageCode`, `VatPercent` the receipt data is left out of `CheckPerformTransaction` (a warning goes to the log once):
the payment works, but the tax office does not see the sale through Payme. Set them before real money.

The address check uses the **last** entry of `X-Forwarded-For` when the connection comes from a private address (our nginx appends the address it
saw; what the caller wrote is first). `deploy/nginx.conf` already passes `X-Forwarded-For` to `/api/`.

### Lines to add to `deploy/docker-compose.prod.yml` (service `api`, `environment:`) - not done on the server

```yaml
      Payme__Login: "${PAYME_LOGIN:-}"
      Payme__Key: "${PAYME_KEY:-}"
      Payme__MerchantId: "${PAYME_MERCHANT_ID:-}"
      Payme__CheckoutUrl: "${PAYME_CHECKOUT_URL:-https://checkout.paycom.uz}"
      Payme__Mxik: "${PAYME_MXIK:-}"
      Payme__PackageCode: "${PAYME_PACKAGE_CODE:-}"
      Payme__VatPercent: "${PAYME_VAT_PERCENT:-}"
```

and the values in the `.env` file next to that compose file on the server (the same way `ADMIN_USERNAMES` and `POSTGRES_PASSWORD` are).
Then `docker compose up -d api`.

## Order of going live

1. Payme opens the cash register (sandbox access first). You give the four things above; they give the cash register id, login and test key.
2. The accountant gives the MXIK code, the package code and the VAT.
3. Prices are set in the admin ("Prices"): without a price a version shows "Soon".
4. Deploy the API (the migration `AddPayme` creates the tables), then the site. Nothing is switched on yet.
5. **Sandbox**: set `Payme__Key` = the TEST_KEY, `Payme__MerchantId`, `Payme__CheckoutUrl=https://test.paycom.uz`, login. In the Payme sandbox
   (`https://test.paycom.uz`, enter the cash register id and the test key) run the scenarios one by one; each one must pass. They are covered by
   our tests (`Jumbo.Tests/PaymeTests.cs`): wrong login/key → -32504; wrong amount → -31001; unknown order → -31050...-31099; allow;
   create + the same call again; a second create for a waiting order → -31008; cancel a created one; perform + the same call again; check;
   cancel a performed one (refund).
6. A real payment of the smallest version with your own card in the sandbox/production, then a refund from the Payme cabinet: the version must
   switch on and off.
7. **Production**: the real key, `Payme__CheckoutUrl` back to the default, `docker compose up -d api`. Make one real payment and refund it.

## Watching it

- Admin → Balance: the list of orders with the state of the Payme transaction (`new`/`waiting`/`paid`/`cancelled`, Payme state 1/2/-1/-2, reason).
- The log of the API: `Payme <Method>: ok` or `Payme: error <code>` per call (never the Authorization header or the key).
- The monitoring script (`deploy/ops/watch-errors.sh`) sees 5xx and `fail:` lines as for any page.
- `payme_transactions` is also what `GetStatement` returns: Payme reconciles with it.

## Open points (not in the documentation that was read)

- the exact login for the Basic authorization (Payme tells it);
- the full list of account error codes in -31050...-31099 (we use -31050 not found, -31051 already paid or cancelled, -31052 version cannot be bought now, -31053 waiting for payment);
- an explicit HTTP timeout of the calls;
- whether the GET checkout link must use `test.paycom.uz` in the sandbox (the sandbox page says so for the POST form; we use the same host);
- the pages "where to find the key", "sample server", "Subscribe API" and "payment button / QR code" were not read.
