package io.github.andivalsei.andivum

import java.time.LocalDate

object FinanceTextImport {
    private val trailingAmount = Regex("(?i)(?<!\\d)(\\d+(?:[.,]\\d{1,3})?)(?!\\d)\\s*(?:₽|RUB|руб)?$")

    fun tryCreateDraft(
        text: String,
        fileName: String,
        categories: Collection<FinanceCategory>,
    ): FinanceDraft? {
        val lines = text.lines().map { it.trim() }.filter { it.isNotBlank() }
        if (lines.isEmpty()) return null
        val subject = lines.firstOrNull { it.startsWith("Subject:", ignoreCase = true) }
            ?.substringAfter(':')?.trim()
            ?.ifBlank { null }
        val items = buildList {
            lines.forEach { line ->
                val fields = when {
                    ';' in line -> line.split(';').map(String::trim)
                    '\t' in line -> line.split('\t').map(String::trim)
                    else -> emptyList()
                }
                val amountText: String
                val itemName: String
                val requestedCategory: String?
                if (fields.size >= 2) {
                    amountText = fields.last()
                    itemName = fields.first()
                    requestedCategory = fields.getOrNull(1)
                } else {
                    val match = trailingAmount.find(line) ?: return@forEach
                    amountText = match.groupValues[1]
                    itemName = line.substring(0, match.range.first).trim(' ', '-', ':')
                    requestedCategory = null
                }
                val amount = runCatching { FinanceMoney.parseMinorUnits(amountText, "RUB") }.getOrNull()
                    ?: return@forEach
                if (amount <= 0L || itemName.isBlank()) return@forEach
                val category = requestedCategory
                    ?.takeIf { slug -> categories.any { it.slug.equals(slug, ignoreCase = true) && it.type == "expense" } }
                    ?: "other.expense"
                add(FinanceDraftItem(itemName, 1.0, amount, category))
            }
        }
        if (items.isEmpty()) return null
        return FinanceDraft(
            type = FinanceTransactionType.EXPENSE,
            title = subject ?: fileName.substringBeforeLast('.', fileName).ifBlank { "Импорт" },
            occurredOn = LocalDate.now().toString(),
            currency = "RUB",
            totalMinor = items.sumOf { it.lineTotalMinor },
            items = items,
        )
    }
}
