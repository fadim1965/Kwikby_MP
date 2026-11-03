# -*- coding: utf-8 -*-
"""Insert captured screenshot references into the Admin/Vendor/User guides.

Run from repo root:  python docs/enhance_guides.py
Writes <name>_ILLUSTRATED.md next to each guide so the originals stay clean.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parent
GUIDE_IMG = "images/guide"

# heading keyword -> list of (image_name, caption)
ADMIN_MAP = {
    "dashboard": [("admin-dashboard", "Admin dashboard")],
    "products": [("admin-products", "Products list"), ("admin-product-create", "Create product"), ("admin-product-edit", "Edit product")],
    "bulk import": [("admin-import", "Bulk import")],
    "categories": [("admin-categories", "Categories"), ("admin-category-create", "Create category"), ("admin-category-edit", "Edit category")],
    "inventory": [("admin-inventory", "Inventory")],
    "orders": [("admin-orders", "Orders list"), ("admin-order-details", "Order detail"), ("admin-order-details-2", "Order detail (second)")],
    "customers": [("admin-customers", "Customers"), ("admin-customer-details", "Customer detail")],
    "returns": [("admin-returns", "Returns queue"), ("admin-return-details", "Return detail")],
    "coupons": [("admin-coupons", "Coupons"), ("admin-coupon-create", "Create coupon"), ("admin-coupon-edit", "Edit coupon")],
    "tax classes": [("admin-tax", "Tax classes")],
    "shops": [("admin-shops", "Shops"), ("admin-verification", "Shop verification")],
    "payouts": [("admin-payouts", "Payouts"), ("admin-payout-details", "Payout detail")],
    "marketing": [("admin-marketing", "Marketing analytics")],
    "reviews moderation": [("admin-reviews", "Reviews moderation"), ("admin-moderation", "Moderation")],
    "email outbox": [("admin-emails", "Email outbox"), ("admin-email-details", "Email detail")],
    "audit trail": [("admin-audit", "Audit trail")],
    "application log": [("admin-settings", "Settings")],
}

VENDOR_MAP = {
    "dashboard": [("seller-dashboard", "Vendor dashboard")],
    "listing products": [("seller-products", "Products"), ("seller-product-create", "Create product"), ("seller-product-edit", "Edit product")],
    "orders": [("seller-orders", "Orders"), ("seller-order-details", "Order detail"), ("seller-order-details-2", "Order detail (second)")],
    "returns": [("seller-returns", "Returns"), ("seller-return-details", "Return detail")],
    "reviews": [("seller-reviews", "Reviews")],
    "payouts": [("seller-payouts", "Payouts"), ("seller-payout-details", "Payout detail")],
    "appearance": [("seller-appearance", "Appearance")],
}

USER_MAP = {
    "finding your way": [("user-home", "Home page")],
    "browsing and searching": [("user-catalog", "Catalogue")],
    "a product page": [("user-product", "Product page")],
    "tracking an order": [("user-track", "Track order"), ("user-order-details", "Order detail")],
    "returning something": [("user-request-return", "Request return"), ("user-order-details", "Order detail")],
    "your account": [("user-login", "Sign in"), ("user-register", "Register"), ("user-forgot-password", "Forgot password"), ("user-profile", "Profile"), ("user-addresses", "Addresses"), ("user-address-edit", "Edit address"), ("user-wishlist", "Wishlist"), ("user-twofactor", "Two-factor")],
    "wishlist": [("user-wishlist", "Wishlist")],
    "reviews": [("user-product", "Product reviews")],
    "shops": [("user-shops", "Shops directory"), ("user-shopfront", "Shop storefront")],
    "language": [],
}

GUIDES = [
    ("ADMIN_GUIDE.md", ADMIN_MAP, "ADMIN_GUIDE_ILLUSTRATED.md"),
    ("VENDOR_GUIDE.md", VENDOR_MAP, "VENDOR_GUIDE_ILLUSTRATED.md"),
    ("USER_GUIDE.md", USER_MAP, "USER_GUIDE_ILLUSTRATED.md"),
]


def match_heading(heading_text, mapping):
    ht = heading_text.lower()
    for key in mapping:
        if key in ht:
            return mapping[key]
    return None


def enhance(md_path, mapping, out_path):
    lines = md_path.read_text(encoding="utf-8").splitlines(keepends=True)
    out = []
    for line in lines:
        out.append(line)
        m = None
        if line.startswith("## ") or line.startswith("### "):
            title = line.lstrip("#").strip()
            imgs = match_heading(title, mapping)
            if imgs:
                out.append("\n")
                for name, cap in imgs:
                    out.append(f"![{cap}]({GUIDE_IMG}/{name}.png)\n")
                    out.append("\n")
    out_path.write_text("".join(out), encoding="utf-8")
    print(f"wrote {out_path.name}")


def main():
    for src, mapping, dst in GUIDES:
        enhance(ROOT / src, mapping, ROOT / dst)


if __name__ == "__main__":
    main()
