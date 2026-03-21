# 5–10 minute demo script (viva / stakeholder)

1. **Login** — Admin / Manager / Cashier (show role-based access).
2. **Items** — Open an item with **Barcode** and **SKU**; mention POS scan uses either.
3. **POS** — New bill → select store/warehouse → on bill screen, **Scan Item** box: type barcode or SKU + Enter (scanner behaves the same).
4. **Line & totals** — Show qty, discounts if applicable; **Complete** bill with a payment method.
5. **Receipt** — Open receipt / PDF if configured.
6. **Stock / audit** — Show stock reduced (Stock Transactions or report) for credibility.
7. **API (optional)** — `/swagger` → authenticate with JWT → call a simple GET (e.g. items) if time permits.

**Closing line:** Tests run in CI (`dotnet test`); health at `/health`; secrets via User Secrets in production.
