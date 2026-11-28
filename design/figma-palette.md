# Palette of the Figma redesign (file "Jumbo.uz — редизайн сайта", page "Design System")

Taken on 2026-11-28 from the Figma file of the colleagues (view-only link). The Figma page has two palettes of 24 swatches (light and dark) with the token names
below. A guest cannot open the Design / Dev panel of Figma, so **the hex values are measured from the rendered swatches** (screenshot of the canvas, the median
colour of the middle of every swatch, JPEG noise about +-3 per channel). They are **estimates**: please compare them with the real values in Figma and correct this
table (and `JumboWeb/JumboWeb/wwwroot/palette-new.css`). Values marked DERIVED are not in the Figma at all: we added them because the site needs them.

How to see the new palette: open any address of the site with `?palette=new` (for example `https://jumbo.uz/?palette=new`). A cookie keeps it for 30 days; the chip
"Palette: new ✕" at the bottom left switches it off (`?palette=old` does the same). Nobody else sees it. The landing preview is `https://jumbo.uz/new`.

## Tokens

| Figma token | Light | Dark | Source | Site variable (`--lp-*`) / use |
|---|---|---|---|---|
| bg/page | `#FFF8EE` | `#100D1A` | estimated | `--lp-bg` page background |
| bg/surface | `#FFFFFF` | `#1B1827` | estimated | `--lp-surface` cards |
| bg/surface-alt | `#FFEED9` | `#252138` | estimated | `--lp-hover-bg` hover / second surface |
| bg/inverse | `#1E1B2D` | `#F6F1FF` | estimated | dark "teachers" card on the landing |
| text/primary | `#1E1B2D` | `#F6F1FF` | estimated | `--lp-text` |
| text/secondary | `#5E5870` | `#ABA3C4` | estimated | `--lp-muted` secondary text |
| text/muted | `#928DA7` | `#746C8D` | estimated | `--lp-disabled-text` (placeholders, disabled) |
| text/inverse | `#FFFFFF` | `#13101D` | estimated | text on the inverse background |
| text/on-brand | `#291700` | `#201100` | estimated | `--lp-on-primary`, `--lp-on-brand` text on orange |
| border/default | `#EFE2CE` | `#2F2B42` | estimated | `--lp-border` |
| border/strong | `#E2CEB3` | `#423D5A` | estimated | `--lp-disabled-edge` |
| brand/primary | `#F18B0E` | `#FD9E2D` | estimated (same as our `#F28C0F`) | `--lp-primary`, `--lp-brand` (light) |
| brand/primary-hover | `#DB7802` | `#FFB25C` | estimated | `--lp-primary-dark`, `--lp-brand-dark` |
| brand/soft | `#FFE6C4` | `#3A2712` | estimated | `--lp-primary-soft` |
| accent/violet | `#6B5BE8` | `#A392FD` | estimated | `--lp-secondary`, landing accents |
| accent/violet-soft | `#ECE8FD` | `#2B244E` | estimated | landing |
| accent/mint | `#18AF9B` | `#49E1BF` | estimated | `--lp-success` |
| accent/mint-soft | `#DAF5F0` | `#13382E` | estimated | `--lp-success-soft` |
| accent/sky | `#2A9BF0` | `#6CC0FA` | estimated (two readings: `#299CF6`, `#2F9AED`) | landing |
| accent/sky-soft | `#DFEFFA` | `#142D49` | estimated | landing |
| accent/pink | `#EE587E` | `#FF86AF` | estimated | `--lp-error` |
| accent/pink-soft | `#FEE2EA` | `#3A1C28` | estimated | `--lp-error-soft` |
| accent/yellow | `#FEBF1A` | `#FDD04E` | estimated | landing |
| accent/yellow-soft | `#FFF1CC` | `#392F17` | estimated | landing |

Typography: Nunito (Display 72 / Heading 1 56 / Heading 2 40 / Body large 20 / Body 16 / Small 14 / Button 16 / Caption 12 in the Figma): the site already uses Nunito.

## DERIVED values (not in the Figma)

| What | Light | Dark | Why |
|---|---|---|---|
| `--lp-primary-text` (the brand colour used as the colour of a text) | `#A15200` | `#FD9E2D` | the brand orange as text on the cream page is only 2.4:1; `#A15200` is 5.3:1 (4.7:1 on brand/soft) |
| `--lp-accent` (JO / lives counters) | `#A15200` | `#FFB25C` | readable on the page (the current site has `#FFB64D`: 1.6:1) |
| `--lp-primary-shadow` (the 3D edge of the chunky buttons) | `#C26A00` | `#C46F00` | the hover colour 12 % darker |
| `--lp-primary-edge` | `#FFCF91` | `#7A4C0A` | between brand/soft and the brand |
| `--lp-success-dark` (mint as a text) | `#0B7D6E` | `#49E1BF` | 5:1 on white |
| pink as a text (errors) | `#B52B54` | `#FF86AF` | 5.8:1 on the page |
| `--lp-brand` in the dark theme (side menu / top bar) | — | `#E8890C` | the current dark frame: the Figma dark `brand/primary` `#FD9E2D` is for buttons, too bright for a big bar |
| text shades of the landing accents (`--nh-*-text`) | sky `#1565A8`, violet `#5B4BD0`, mint `#09665A`, pink `#B52B54`, yellow `#7A5200` | the dark accents themselves | the accents as text on a light card are 1.7-3:1 |

The Figma has **no dark frames** of the pages (only the dark swatches): the dark landing and the dark look of every other page are made by us from these swatches.
The side menu and the top bar stay orange (the Figma landing has no application chrome).

## What the preview does not change

- The colours of the **level of a book** (A1 red, A2 yellow, B1 green, B2 violet: `data-level`) on the book, topic and game pages.
- The themes **bought in the market** (`data-palette`), the avatar frames, the pictures, the inline SVG of avatars / pets / the tiger, and every colour that is written in a style sheet as a hex
  value instead of a variable (about 9 % of the rules: see the report in the commit message of the palette preview).
