# -*- coding: utf-8 -*-
"""Capture the screenshots used by the Admin, Seller and User guides.

Run from the repository root:  python docs/capture_guide.py

The app must be running at http://localhost:5268 with the standard demo seed.
Screenshots land in docs/images/guide/.  Pages taller than FULL_PAGE_LIMIT are
captured as a viewport shot (the top of the page) so the PDF stays readable.
"""
from __future__ import annotations

import os
import sys
from pathlib import Path

from playwright.sync_api import sync_playwright

BASE = os.environ.get("KWIKBY_URL", "http://localhost:5268")
ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "docs" / "images" / "guide"
OUT.mkdir(parents=True, exist_ok=True)

ADMIN = ("admin@libanpost.example", "Admin#12345")
CUSTOMER = ("customer@libanpost.example", "Customer#123")

FULL_PAGE_LIMIT = 2600

# ---------------------------------------------------------------- page sets
ADMIN_PAGES = [
    ("admin-dashboard", "/Admin"),
    ("admin-products", "/Admin/Products"),
    ("admin-product-create", "/Admin/Products/Create"),
    ("admin-product-edit", "/Admin/Products/Edit/1"),
    ("admin-import", "/Admin/Import"),
    ("admin-categories", "/Admin/Categories"),
    ("admin-category-create", "/Admin/Categories/Create"),
    ("admin-category-edit", "/Admin/Categories/Edit/1"),
    ("admin-inventory", "/Admin/Inventory"),
    ("admin-orders", "/Admin/Orders"),
    ("admin-order-details", "/Admin/Orders/Details/1"),
    ("admin-order-details-2", "/Admin/Orders/Details/9"),
    ("admin-returns", "/Admin/Returns"),
    ("admin-return-details", "/Admin/Returns/Details/1"),
    ("admin-postsale", "/Admin/PostSale"),
    ("admin-postsale-rts", "/Admin/PostSale/Rts"),
    ("admin-postsale-fulfilments", "/Admin/PostSale/Fulfilments"),
    ("admin-customers", "/Admin/Customers"),
    ("admin-customer-details", "/Admin/Customers/Details/83edf551b85046b8b1d57a569d0a1ebf"),
    ("admin-coupons", "/Admin/Coupons"),
    ("admin-coupon-create", "/Admin/Coupons/Create"),
    ("admin-coupon-edit", "/Admin/Coupons/Edit/1"),
    ("admin-tax", "/Admin/Tax"),
    ("admin-shops", "/Admin/Shops"),
    ("admin-verification", "/Admin/Verification"),
    ("admin-moderation", "/Admin/Moderation"),
    ("admin-payouts", "/Admin/Payouts"),
    ("admin-payout-details", "/Admin/Payouts/Details/6"),
    ("admin-marketing", "/Admin/Marketing"),
    ("admin-reviews", "/Admin/Reviews"),
    ("admin-reports", "/Admin/Reports"),
    ("admin-report-sales", "/Admin/Reports/Report?key=sales"),
    ("admin-report-inventory", "/Admin/Reports/Report?key=inventory"),
    ("admin-emails", "/Admin/Emails"),
    ("admin-email-details", "/Admin/Emails/Details/31"),
    ("admin-audit", "/Admin/Audit"),
    ("admin-settings", "/Admin/Settings"),
    ("admin-translations", "/Admin/Translations"),
    ("admin-cod-remittances", "/Admin/CodRemittances"),
    ("admin-cod-create", "/Admin/CodRemittances/Create"),
]

SELLER_PAGES = [
    ("seller-dashboard", "/Vendor/Dashboard"),
    ("seller-products", "/Vendor/Products"),
    ("seller-product-create", "/Vendor/Products/Create"),
    ("seller-product-edit", "/Vendor/Products/Edit/2"),
    ("seller-appearance", "/Vendor/Appearance"),
    ("seller-orders", "/Vendor/Orders"),
    ("seller-order-details", "/Vendor/Orders/Details/1"),
    ("seller-order-details-2", "/Vendor/Orders/Details/9"),
    ("seller-returns", "/Vendor/Returns"),
    ("seller-return-details", "/Vendor/Returns/Details/1"),
    ("seller-postsale", "/Vendor/PostSale"),
    ("seller-fulfilments", "/Vendor/PostSale/Fulfilments"),
    ("seller-messages", "/Vendor/Messages"),
    ("seller-message-details", "/Vendor/Messages/Details/1"),
    ("seller-reviews", "/Vendor/Reviews"),
    ("seller-payouts", "/Vendor/Payouts"),
    ("seller-payout-details", "/Vendor/Payouts/Details/6"),
    ("seller-reports", "/Vendor/Reports"),
    ("seller-report-sales", "/Vendor/Reports/Report?key=sales"),
    ("seller-team", "/Vendor/Team"),
]

CUSTOMER_PAGES = [
    ("user-home", "/"),
    ("user-catalog", "/Catalog"),
    ("user-product", "/product/ems-priority-envelope-pack-of-10"),
    ("user-shops", "/Shops"),
    ("user-shopfront", "/shop/libanpost-eshop"),
    ("user-track", "/Orders/Track"),
    ("user-login", "/Account/Login"),
    ("user-register", "/Account/Register"),
    ("user-forgot-password", "/Account/ForgotPassword"),
    ("user-profile", "/Account/Profile"),
    ("user-addresses", "/Account/Addresses"),
    ("user-address-edit", "/Account/EditAddress"),
    ("user-wishlist", "/Account/Wishlist"),
    ("user-orders", "/Orders"),
    ("user-order-details", "/Orders/Details/1"),
    ("user-request-return", "/Orders/RequestReturn/1"),
    ("user-messages", "/Messages"),
    ("user-message-details", "/Messages/Details/1"),
    ("user-postsale", "/PostSale"),
    ("user-postsale-create", "/PostSale/Create?orderId=1"),
    ("user-twofactor", "/Account/TwoFactor"),
]


def login(page, email: str, password: str) -> None:
    page.goto(f"{BASE}/Account/Login", wait_until="networkidle")
    if "/Account/Profile" in page.url or page.locator("input[name=Password]").count() == 0:
        return
    page.fill("input[name=Email]", email)
    page.fill("input[name=Password]", password)
    page.click("form:has(input[name=Password]) button[type=submit]")
    page.wait_for_load_state("networkidle")
    page.wait_for_timeout(600)


def capture(page, name: str, path: str) -> str:
    url = BASE + path
    try:
        page.goto(url, wait_until="networkidle", timeout=45000)
    except Exception:
        page.goto(url, wait_until="load", timeout=45000)
    page.wait_for_timeout(900)
    height = page.evaluate("Math.max(document.documentElement.scrollHeight, document.body.scrollHeight)")
    full = height <= FULL_PAGE_LIMIT
    dest = OUT / f"{name}.png"
    page.screenshot(path=str(dest), full_page=full)
    return f"{name}\t{'full' if full else 'viewport'}\t{height}\t{dest}"


def run(pages, email, password, label):
    print(f"\n=== {label} ===", flush=True)
    with sync_playwright() as p:
        browser = p.chromium.launch(channel="msedge", headless=True)
        context = browser.new_context(
            viewport={"width": 1440, "height": 900},
            device_scale_factor=1,
            locale="en-GB",
        )
        page = context.new_page()
        login(page, email, password)
        print(f"logged in -> {page.url}", flush=True)
        for name, path in pages:
            try:
                print(capture(page, name, path), flush=True)
            except Exception as exc:
                print(f"FAILED {name} {path}: {exc}", flush=True)
        browser.close()


def main():
    only = set(sys.argv[1:])
    if not only or "admin" in only:
        run(ADMIN_PAGES, *ADMIN, "ADMIN")
    if not only or "seller" in only:
        run(SELLER_PAGES, *ADMIN, "SELLER")
    if not only or "user" in only:
        run(CUSTOMER_PAGES, *CUSTOMER, "USER")
    print("\nDone.")


if __name__ == "__main__":
    main()
