package io.github.andivalsei.andivum

enum class FinanceTransactionType {
    INCOME,
    EXPENSE,
    TRANSFER,
}

data class FinanceDraft(
    val type: FinanceTransactionType,
    val title: String,
    val occurredOn: String,
    val currency: String,
    val totalMinor: Long,
    val items: List<FinanceDraftItem>,
)

data class FinanceDraftItem(
    val name: String,
    val quantity: Double,
    val lineTotalMinor: Long,
    val categorySlug: String,
) {
    val unitPriceMinor: Long
        get() = if (quantity <= 0.0) 0L else
            java.math.BigDecimal.valueOf(lineTotalMinor.toDouble())
                .divide(java.math.BigDecimal.valueOf(quantity), 0, java.math.RoundingMode.HALF_UP)
                .longValueExact()
}

data class FinanceCategory(
    val id: String,
    val slug: String,
    val name: String,
    val type: String,
    val parentId: String?,
)

data class FinanceAccount(
    val id: String,
    val name: String,
    val accountType: String,
    val currency: String,
)

data class FinanceTransaction(
    val id: String,
    val title: String,
    val type: String,
    val occurredOn: String,
    val currency: String,
    val totalMinor: Long,
    val source: String,
    val items: List<FinanceDraftItem>,
)
