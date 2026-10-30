# Categories and shops scripts

Run in order against the KwikbyMP database, after the schema has been created.

1. `04_Categories.sql`: creates the 11 global categories if they don't exist.
2. Create the three owner accounts (`owner.northwind@example.com`, `owner.lumen@example.com`, `owner.atlas@example.com`) through the app's sign-up page. Change the emails in `05_Shops.sql` if you use others.
3. `05_Shops.sql`: creates the three approved shops and makes each owner the shop owner. It stops with a message if an owner account is missing.

Both scripts are safe to re-run: they skip rows that already exist.
