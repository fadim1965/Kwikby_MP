# Kwikby design system (step 1: theme)

## Files
- `wwwroot/css/kwikby.css`: all colours, type, spacing and components. Colours are CSS variables in `:root`.
- `wwwroot/js/kwikby.js`: progressive enhancement (search suggestions, favourites, gallery, quantity, filter drawer, toast).
- `wwwroot/images/kwikby-logo.png`: the logo from the brand file.
- `preview.html`: a standalone page showing the header, category row, hero, browse tiles, filter sidebar and product grid. Open it in a browser; no build step needed.

## Palette (from the logo)
| Token | Value | Use |
|---|---|---|
| `--kw-blue` | `#315d9e` | Logo blue: search button, links, brand bars |
| `--kw-green` | `#3baf29` | Logo green: primary buttons, cart count, toggles |
| `--kw-ink` | `#1f2a37` | Body text |
| `--kw-surface-muted` | `#f6f7f9` | Page background |

## Using it in ASP.NET (when the project is created)
Place the `wwwroot` folder in `KwikbyMP`, then in `_Layout.cshtml` add:

```html
<link rel="stylesheet" href="~/css/kwikby.css" asp-append-version="true" />
...
<script src="~/js/kwikby.js" asp-append-version="true" defer></script>
```

Hooks used by the script:
- `data-kw-suggest` with `data-url` on the search input. The endpoint should return a JSON array of `{ type, name, url }`. Check this against the existing `/Catalog/Suggest` response.
- `data-kw-fav` with an optional `data-url` to save favourites.
- `data-kw-toggle="kw-filters"` on the mobile Filters button.
- `data-kw-autosubmit` on the sort `<select>`.

## Not in this step
Razor layouts and partial views, the product and shop pages, cart and checkout styling, and the real logo at favicon sizes. These come next, once you approve the look in `preview.html`.

## Navigation (step 2)
- **Categories** (hamburger): a panel under the button listing the top-level categories, plus "All categories". It opens on click, Enter or Space, and closes on Escape or an outside click.
- **Mega menu**: New arrivals, Deals, Gifts, Shops and Brands each open a full-width panel with three link columns and a promo card. Sell is a plain link.
- Mouse users can also open a menu by hovering. Keyboard users can press Down Arrow on a trigger to move into its panel.
- On phones, the Categories panel and the mega panels fill the screen below the header.
- Hooks: `data-kw-menu-trigger` on each button, with `aria-controls` naming its panel. A panel with `hidden` stays closed when JavaScript is off.

## Home page and responsive behaviour (step 3)
- Home sections, in order: hero, shop by category, hot deals (with countdown), most popular, best for this season, new arrivals, top shops, gift guides, trust strip.
- Phones (below 768px): the first five sections show. The rest sit behind "See more sections" (`data-kw-more`).
- Product grid: 2 columns on phones and tablets, 4 on desktop. Shop cards and gift tiles reflow the same way.
- Navigation panels anchor below the nav bar on phones and are capped at 70–75% of the screen height, so they scroll.
- Tested in headless Chromium at 360, 414, 600, 768, 900, 1024, 1280 and 1440px: no horizontal overflow, panels stay inside the screen, and menus, Escape, one-menu-at-a-time and See more all work.
- Known trade-off: the sticky header (logo, search and nav) is about 166px tall on phones. Consider making it non-sticky on phones if the content area feels cramped.

## Home page update (step 4)
- "Shop by category" is removed from the home page. The main menu's Categories panel now covers that job.
- "Picked for you" replaces it: quick chips (All, Free shipping, On sale, Star sellers, Under $25), a category dropdown and a sort dropdown in the toolbar, and an eight-product grid. There is no filter sidebar on the home page.
- Home sections, in order: hero, hot deals, most popular, picked for you, best for this season, new arrivals, top shops, gift guides, trust strip. On phones the first five (hero plus four) show.
- The chips are links with `aria-current` for the active one. The dropdowns use `data-kw-autosubmit`, so they work with or without JavaScript.

## Hero carousel (step 5)
- Four slots, rotating every 6 seconds: Kwikby picks (platform fallback), a campaign, a shop spotlight and a category spotlight. Each slide has a visible label so shoppers can tell a featured vendor from a platform message.
- Controls: dots, previous and next arrows (hidden on phones; use dots or swipe later), and pause/play.
- Autoplay stops while the pointer or keyboard focus is inside the banner, while the tab is hidden, when the visitor pauses it, and for users with reduced motion on. Left and Right arrow keys move between slides.
- Hooks: `data-kw-carousel` on the banner, `data-kw-slide` on each slide, `data-kw-dot`, `data-kw-prev`, `data-kw-next`, `data-kw-pause`.
- In production the slide order will come from the weighted scheduler described in the hero proposal; the markup stays the same.
