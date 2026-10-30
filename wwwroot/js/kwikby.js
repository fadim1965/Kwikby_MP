/* ==========================================================================
   kwikby.js: progressive enhancement for the Kwikby storefront.

   The site works without JavaScript (forms submit, links navigate). This file
   adds: search suggestions, favourite toggles, the product gallery, quantity
   steppers, the mobile filter drawer, and a small toast for feedback.

   Hooks are data attributes, so Razor markup stays free of inline script:
     [data-kw-suggest]          search input; suggestions load from data-url
     [data-kw-fav]              favourite button; data-url (optional) saves it
     [data-kw-thumb]            gallery thumbnail; data-src is the full image
     [data-kw-qty]              quantity wrapper with [data-kw-dec] and [data-kw-inc]
     [data-kw-toggle="id"]      opens/closes the element with that id
     [data-kw-autosubmit]       a <select> that submits its form on change
     [data-kw-menu-trigger]     button whose aria-controls names a menu panel (Categories, mega menu)
     [data-kw-countdown]        element with data-kw-countdown="ISO end time"; shows the time left
     [data-kw-more]             button whose aria-controls names the home page; toggles the lower sections on phones
     [data-kw-carousel]         hero banner; slides carry data-kw-slide, dots data-kw-dot, and controls data-kw-prev/next/pause
   ========================================================================== */
(function () {
    "use strict";

    var SUGGEST_DELAY_MS = 220;
    var MIN_QUERY_LENGTH = 2;

    /* ---------- helpers ------------------------------------------------- */

    function $(selector, root) {
        return (root || document).querySelector(selector);
    }

    function $$(selector, root) {
        return Array.prototype.slice.call((root || document).querySelectorAll(selector));
    }

    function getAntiforgeryToken() {
        var field = document.querySelector('input[name="__RequestVerificationToken"]');
        return field ? field.value : "";
    }

    /** Shows a short message at the bottom of the screen. */
    function toast(message) {
        var el = $("#kw-toast");
        if (!el) {
            el = document.createElement("div");
            el.id = "kw-toast";
            el.className = "kw-toast";
            el.setAttribute("role", "status");
            el.setAttribute("aria-live", "polite");
            document.body.appendChild(el);
        }
        el.textContent = message;
        el.classList.add("is-visible");
        clearTimeout(el._hideTimer);
        el._hideTimer = setTimeout(function () {
            el.classList.remove("is-visible");
        }, 2400);
    }

    /** Sends a JSON POST with the anti-forgery token. Resolves to the parsed response or null. */
    function postJson(url, body) {
        return fetch(url, {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": getAntiforgeryToken()
            },
            body: JSON.stringify(body || {})
        }).then(function (res) {
            if (!res.ok) {
                throw new Error("HTTP " + res.status);
            }
            return res.headers.get("content-type") && res.headers.get("content-type").indexOf("json") > -1
                ? res.json()
                : null;
        });
    }

    /* ---------- search suggestions -------------------------------------- */

    /**
     * The catalogue endpoint returns { products: [{name, slug, price, shop}], categories: [{name, slug}] }.
     * Convert that into the flat list render() expects, building each link from the slug.
     */
    function toItems(data) {
        if (Array.isArray(data)) {
            return data;
        }
        var items = [];
        ((data && data.categories) || []).forEach(function (c) {
            items.push({ type: "Department", name: c.name, url: "/Catalog?category=" + encodeURIComponent(c.slug) });
        });
        ((data && data.products) || []).forEach(function (p) {
            items.push({
                type: "Product",
                name: p.shop ? p.name + " · " + p.shop : p.name,
                url: "/Products/Details?slug=" + encodeURIComponent(p.slug)
            });
        });
        return items;
    }

    /**
     * Expects the suggest endpoint to return a JSON array of
     * { type, name, url }, or the { products, categories } shape the catalogue returns.
     */
    function initSuggest(input) {
        var list = document.getElementById(input.getAttribute("aria-controls") || "");
        var endpoint = input.getAttribute("data-url");
        if (!list || !endpoint) {
            return;
        }

        var timer = null;
        var controller = null;
        var activeIndex = -1;

        function close() {
            list.classList.remove("is-open");
            input.setAttribute("aria-expanded", "false");
            activeIndex = -1;
        }

        function items() {
            return $$("li[role='option']", list);
        }

        function setActive(index) {
            var all = items();
            all.forEach(function (li) {
                li.classList.remove("is-active");
                li.setAttribute("aria-selected", "false");
            });
            if (index < 0 || index >= all.length) {
                activeIndex = -1;
                return;
            }
            activeIndex = index;
            all[index].classList.add("is-active");
            all[index].setAttribute("aria-selected", "true");
        }

        /** Builds one suggestion row using text nodes only, so names cannot inject markup. */
        function render(results) {
            list.textContent = "";
            if (!results || results.length === 0) {
                close();
                return;
            }
            results.slice(0, 8).forEach(function (r) {
                var li = document.createElement("li");
                li.className = "kw-suggest__item";
                li.setAttribute("role", "option");
                li.setAttribute("aria-selected", "false");

                var a = document.createElement("a");
                a.href = r.url || "#";

                var type = document.createElement("span");
                type.className = "kw-suggest__type";
                type.textContent = r.type || "";

                var name = document.createElement("span");
                name.textContent = r.name || "";

                a.appendChild(type);
                a.appendChild(name);
                li.appendChild(a);
                list.appendChild(li);
            });
            list.classList.add("is-open");
            input.setAttribute("aria-expanded", "true");
        }

        function fetchSuggestions(query) {
            if (controller) {
                controller.abort();
            }
            controller = typeof AbortController !== "undefined" ? new AbortController() : null;
            var url = endpoint + (endpoint.indexOf("?") > -1 ? "&" : "?") + "q=" + encodeURIComponent(query);
            fetch(url, {
                credentials: "same-origin",
                headers: { "Accept": "application/json" },
                signal: controller ? controller.signal : undefined
            })
                .then(function (res) { return res.ok ? res.json() : null; })
                .then(function (data) { render(toItems(data)); })
                .catch(function (err) {
                    if (err && err.name !== "AbortError") {
                        close();
                    }
                });
        }

        input.addEventListener("input", function () {
            var query = input.value.trim();
            clearTimeout(timer);
            if (query.length < MIN_QUERY_LENGTH) {
                close();
                return;
            }
            timer = setTimeout(function () { fetchSuggestions(query); }, SUGGEST_DELAY_MS);
        });

        input.addEventListener("keydown", function (e) {
            if (!list.classList.contains("is-open")) {
                return;
            }
            if (e.key === "ArrowDown") {
                e.preventDefault();
                setActive(Math.min(activeIndex + 1, items().length - 1));
            } else if (e.key === "ArrowUp") {
                e.preventDefault();
                setActive(Math.max(activeIndex - 1, 0));
            } else if (e.key === "Enter" && activeIndex > -1) {
                e.preventDefault();
                var link = $("a", items()[activeIndex]);
                if (link) {
                    window.location.href = link.href;
                }
            } else if (e.key === "Escape") {
                close();
            }
        });

        document.addEventListener("click", function (e) {
            if (!input.contains(e.target) && !list.contains(e.target)) {
                close();
            }
        });
    }

    /* ---------- favourites ---------------------------------------------- */

    function initFavourites(button) {
        button.addEventListener("click", function (e) {
            e.preventDefault();
            e.stopPropagation();

            var pressed = button.getAttribute("aria-pressed") === "true";
            var next = !pressed;
            button.setAttribute("aria-pressed", String(next));
            button.setAttribute("aria-label", next ? "Remove from favourites" : "Add to favourites");

            var url = button.getAttribute("data-url");
            if (!url) {
                toast(next ? "Saved to favourites" : "Removed from favourites");
                return;
            }

            postJson(url, { favourite: next })
                .then(function () {
                    toast(next ? "Saved to favourites" : "Removed from favourites");
                })
                .catch(function () {
                    // Roll back the visual state if the server did not accept the change.
                    button.setAttribute("aria-pressed", String(pressed));
                    button.setAttribute("aria-label", pressed ? "Remove from favourites" : "Add to favourites");
                    toast("Could not update favourites. Please sign in and try again.");
                });
        });
    }

    /* ---------- product gallery ----------------------------------------- */

    function initGallery(thumb) {
        thumb.addEventListener("click", function () {
            var main = document.querySelector("[data-kw-main]");
            var src = thumb.getAttribute("data-src");
            var alt = thumb.getAttribute("data-alt") || "";
            if (!main || !src) {
                return;
            }
            var img = main.querySelector("img");
            if (img) {
                img.src = src;
                img.alt = alt;
            }
            $$("[data-kw-thumb]").forEach(function (t) {
                t.setAttribute("aria-pressed", t === thumb ? "true" : "false");
            });
        });
    }

    /* ---------- quantity stepper ---------------------------------------- */

    function initQuantity(wrapper) {
        var input = $("input", wrapper);
        var dec = $("[data-kw-dec]", wrapper);
        var inc = $("[data-kw-inc]", wrapper);
        if (!input) {
            return;
        }

        var min = parseInt(input.getAttribute("min") || "1", 10);
        var max = parseInt(input.getAttribute("max") || "99", 10);

        function clamp(value) {
            if (isNaN(value)) {
                return min;
            }
            return Math.max(min, Math.min(max, value));
        }

        function set(value) {
            input.value = String(clamp(value));
            input.dispatchEvent(new Event("change", { bubbles: true }));
        }

        if (dec) {
            dec.addEventListener("click", function () { set(parseInt(input.value, 10) - 1); });
        }
        if (inc) {
            inc.addEventListener("click", function () { set(parseInt(input.value, 10) + 1); });
        }
        input.addEventListener("change", function () { set(parseInt(input.value, 10)); });
    }

    /* ---------- mobile filter drawer and generic toggles ---------------- */

    function initToggle(button) {
        var targetId = button.getAttribute("data-kw-toggle");
        var target = document.getElementById(targetId);
        if (!target) {
            return;
        }
        button.setAttribute("aria-controls", targetId);
        button.setAttribute("aria-expanded", "false");

        button.addEventListener("click", function () {
            var open = !target.classList.contains("is-open");
            target.classList.toggle("is-open", open);
            button.setAttribute("aria-expanded", String(open));
        });
    }

    /* ---------- sort and filter forms ----------------------------------- */

    function initAutoSubmit(select) {
        select.addEventListener("change", function () {
            if (select.form) {
                select.form.submit();
            }
        });
    }

    /* ---------- navigation menus (Categories and mega menu) ------------- */

    /**
     * Each trigger has aria-controls pointing at its panel. Click toggles it.
     * On mouse devices, hovering the trigger or panel also opens it, and leaving
     * both closes it after a short delay. Only one menu is open at a time.
     */
    var CLOSE_DELAY_MS = 180;

    function initMenus() {
        var triggers = $$("[data-kw-menu-trigger]");
        if (!triggers.length) {
            return;
        }

        var hoverCapable = window.matchMedia && window.matchMedia("(hover: hover) and (pointer: fine)").matches;

        function panelOf(trigger) {
            return document.getElementById(trigger.getAttribute("aria-controls") || "");
        }

        function isOpen(trigger) {
            return trigger.getAttribute("aria-expanded") === "true";
        }

        function open(trigger) {
            triggers.forEach(function (t) {
                if (t !== trigger) {
                    close(t, false);
                }
            });
            var panel = panelOf(trigger);
            if (!panel) {
                return;
            }
            clearTimeout(trigger._closeTimer);
            panel.hidden = false;
            trigger.setAttribute("aria-expanded", "true");
        }

        function close(trigger, restoreFocus) {
            var panel = panelOf(trigger);
            clearTimeout(trigger._closeTimer);
            if (panel) {
                panel.hidden = true;
            }
            trigger.setAttribute("aria-expanded", "false");
            if (restoreFocus) {
                trigger.focus();
            }
        }

        function scheduleClose(trigger) {
            clearTimeout(trigger._closeTimer);
            trigger._closeTimer = setTimeout(function () {
                if (!panelOf(trigger) || !isOpen(trigger)) {
                    return;
                }
                close(trigger, false);
            }, CLOSE_DELAY_MS);
        }

        triggers.forEach(function (trigger) {
            var panel = panelOf(trigger);
            if (!panel) {
                return;
            }
            panel.hidden = true;
            trigger.setAttribute("aria-expanded", "false");

            trigger.addEventListener("click", function (e) {
                e.preventDefault();
                // A hover that already opened this menu should not be undone by the click that follows it.
                if (trigger._hoverOpened && isOpen(trigger)) {
                    trigger._hoverOpened = false;
                    return;
                }
                if (isOpen(trigger)) {
                    close(trigger, false);
                } else {
                    open(trigger);
                }
            });

            if (hoverCapable) {
                trigger.addEventListener("mouseenter", function () {
                    if (!isOpen(trigger)) {
                        open(trigger);
                        trigger._hoverOpened = true;
                    }
                });
                trigger.addEventListener("mouseleave", function () { trigger._hoverOpened = false; scheduleClose(trigger); });
                panel.addEventListener("mouseenter", function () { clearTimeout(trigger._closeTimer); });
                panel.addEventListener("mouseleave", function () { scheduleClose(trigger); });
            }

            trigger.addEventListener("keydown", function (e) {
                if (e.key === "ArrowDown" && isOpen(trigger)) {
                    var first = $("a", panel);
                    if (first) {
                        e.preventDefault();
                        first.focus();
                    }
                }
            });

            panel.addEventListener("keydown", function (e) {
                if (e.key === "Escape") {
                    close(trigger, true);
                }
            });
        });

        document.addEventListener("click", function (e) {
            triggers.forEach(function (trigger) {
                var panel = panelOf(trigger);
                if (isOpen(trigger) && panel && !panel.contains(e.target) && !trigger.contains(e.target)) {
                    close(trigger, false);
                }
            });
        });

        document.addEventListener("keydown", function (e) {
            if (e.key !== "Escape") {
                return;
            }
            triggers.forEach(function (trigger) {
                if (isOpen(trigger)) {
                    close(trigger, true);
                }
            });
        });
    }

    /* ---------- deal countdowns and "See more" on home ------------------- */

    function formatRemaining(ms) {
        if (ms <= 0) {
            return "Deal ended";
        }
        var total = Math.floor(ms / 1000);
        var days = Math.floor(total / 86400);
        var hours = Math.floor((total % 86400) / 3600);
        var mins = Math.floor((total % 3600) / 60);
        var secs = total % 60;
        var pad = function (n) { return (n < 10 ? "0" : "") + n; };
        var clock = pad(hours) + ":" + pad(mins) + ":" + pad(secs);
        return days > 0 ? "Ends in " + days + "d " + clock : "Ends in " + clock;
    }

    function initCountdown(el) {
        var end = Date.parse(el.getAttribute("data-kw-countdown"));
        if (isNaN(end)) {
            return;
        }
        function tick() {
            var remaining = end - Date.now();
            el.textContent = formatRemaining(remaining);
            if (remaining <= 0) {
                clearInterval(el._kwTimer);
            }
        }
        tick();
        el._kwTimer = setInterval(tick, 1000);
    }

    function initMore(button) {
        var root = document.getElementById(button.getAttribute("aria-controls") || "");
        if (!root) {
            return;
        }
        button.addEventListener("click", function () {
            var open = !root.classList.contains("is-expanded");
            root.classList.toggle("is-expanded", open);
            button.setAttribute("aria-expanded", String(open));
            button.textContent = open ? "Show fewer sections" : "See more sections";
        });
    }

    /* ---------- hero carousel -------------------------------------------- */

    var HERO_INTERVAL_MS = 6000;

    /**
     * Rotates the hero slides. Stops for reduced-motion users, when the visitor
     * presses pause, while the pointer or keyboard focus is inside the banner,
     * and while the tab is hidden. Left and Right arrows move between slides.
     */
    function initCarousel(root) {
        var slides = $$("[data-kw-slide]", root);
        var dots = $$("[data-kw-dot]", root);
        var pause = $("[data-kw-pause]", root);
        var prev = $("[data-kw-prev]", root);
        var next = $("[data-kw-next]", root);
        if (slides.length < 2) {
            return;
        }

        var current = 0;
        var timer = null;
        var userPaused = false;
        var hovering = false;
        var reduceMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

        function show(index) {
            current = (index + slides.length) % slides.length;
            slides.forEach(function (slide, i) {
                var active = i === current;
                slide.classList.toggle("is-active", active);
                slide.setAttribute("aria-hidden", String(!active));
            });
            dots.forEach(function (dot, i) {
                dot.setAttribute("aria-current", String(i === current));
            });
        }

        function running() {
            return !userPaused && !hovering && !reduceMotion && !document.hidden;
        }

        function stop() {
            clearInterval(timer);
            timer = null;
        }

        function start() {
            stop();
            if (running()) {
                timer = setInterval(function () { show(current + 1); }, HERO_INTERVAL_MS);
            }
        }

        function setPaused(value) {
            userPaused = value;
            if (pause) {
                pause.setAttribute("aria-pressed", String(value));
                pause.setAttribute("aria-label", value ? "Play slideshow" : "Pause slideshow");
            }
            start();
        }

        dots.forEach(function (dot, i) {
            dot.addEventListener("click", function () { show(i); start(); });
        });
        if (prev) {
            prev.addEventListener("click", function () { show(current - 1); start(); });
        }
        if (next) {
            next.addEventListener("click", function () { show(current + 1); start(); });
        }
        if (pause) {
            pause.addEventListener("click", function () { setPaused(!userPaused); });
        }

        root.addEventListener("mouseenter", function () { hovering = true; start(); });
        root.addEventListener("mouseleave", function () { hovering = false; start(); });
        root.addEventListener("focusin", function () { hovering = true; start(); });
        root.addEventListener("focusout", function () { hovering = false; start(); });

        root.addEventListener("keydown", function (e) {
            if (e.key === "ArrowRight") {
                e.preventDefault();
                show(current + 1);
                start();
            } else if (e.key === "ArrowLeft") {
                e.preventDefault();
                show(current - 1);
                start();
            }
        });

        document.addEventListener("visibilitychange", start);

        if (reduceMotion && pause) {
            pause.setAttribute("aria-pressed", "true");
            pause.setAttribute("aria-label", "Play slideshow");
            userPaused = true;
        }
        show(0);
        start();
    }

    /* ---------- boot ----------------------------------------------------- */

    function ready(fn) {
        if (document.readyState === "loading") {
            document.addEventListener("DOMContentLoaded", fn);
        } else {
            fn();
        }
    }

    ready(function () {
        $$("[data-kw-suggest]").forEach(initSuggest);
        $$("[data-kw-fav]").forEach(initFavourites);
        $$("[data-kw-thumb]").forEach(initGallery);
        $$("[data-kw-qty]").forEach(initQuantity);
        $$("[data-kw-toggle]").forEach(initToggle);
        $$("select[data-kw-autosubmit]").forEach(initAutoSubmit);
        initMenus();
        $$("[data-kw-carousel]").forEach(initCarousel);
        $$("[data-kw-countdown]").forEach(initCountdown);
        $$("[data-kw-more]").forEach(initMore);
    });
})();
