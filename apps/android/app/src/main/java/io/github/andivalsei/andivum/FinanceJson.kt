package io.github.andivalsei.andivum

import java.math.BigDecimal
import java.math.RoundingMode
import org.json.JSONArray
import org.json.JSONObject

object FinanceJson {
    fun createTransactionPayload(
        draft: FinanceDraft,
        accountId: String,
        source: String = "manual",
        importFingerprint: String? = null,
    ): JSONObject {
        require(accountId.isNotBlank()) { "Account is required." }
        require(draft.title.isNotBlank()) { "Transaction title is required." }
        require(draft.totalMinor > 0L && draft.items.isNotEmpty()) {
            "Transaction must have a positive total and at least one item."
        }
        require(draft.items.sumOf { it.lineTotalMinor } == draft.totalMinor) {
            "Items must equal the transaction total."
        }

        val items = JSONArray()
        draft.items.forEach { item ->
            require(item.name.isNotBlank()) { "Transaction item name is required." }
            require(item.quantity > 0.0 && item.lineTotalMinor >= 0L) {
                "Transaction item amount is invalid."
            }
            items.put(
                JSONObject()
                    .put("name", item.name.trim())
                    .put("quantity", item.quantity)
                    .put("unit_price_minor", item.unitPriceMinor)
                    .put("line_total_minor", item.lineTotalMinor)
                    .put("category_slug", item.categorySlug),
            )
        }

        return JSONObject()
            .put("account_id", accountId)
            .put("type", draft.type.name.lowercase())
            .put("title", draft.title.trim())
            .put("occurred_on", draft.occurredOn)
            .put("currency", draft.currency.uppercase())
            .put("total_minor", draft.totalMinor)
            .put("source", source)
            .putOpt("import_fingerprint", importFingerprint)
            .put("items", items)
    }
}

object FinanceMoney {
    fun parseMinorUnits(value: String, currency: String): Long {
        val normalized = value.trim()
            .replace(" ", "")
            .replace(',', '.')
        require(normalized.isNotBlank()) { "Amount is required." }
        val scale = when (currency.uppercase()) {
            "BHD", "IQD", "JOD", "KWD", "LYD", "OMR", "TND" -> 3
            "CLP", "DJF", "GNF", "ISK", "JPY", "KMF", "KRW", "PYG", "RWF", "UGX", "VND", "VUV", "XAF", "XOF", "XPF" -> 0
            else -> 2
        }
        return BigDecimal(normalized)
            .movePointRight(scale)
            .setScale(0, RoundingMode.HALF_UP)
            .longValueExact()
    }
}
