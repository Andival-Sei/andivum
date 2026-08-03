package io.github.andivalsei.andivum

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class FinanceJsonTest {
    @Test
    fun createPayload_keeps_minor_units_and_item_categories() {
        val draft = FinanceDraft(
            type = FinanceTransactionType.EXPENSE,
            title = "Покупки",
            occurredOn = "2026-08-03",
            currency = "RUB",
            totalMinor = 1234,
            items = listOf(
                FinanceDraftItem("Молоко", 1.0, 1234, "food.dairy"),
            ),
        )

        val payload = FinanceJson.createTransactionPayload(draft, "account-1")

        assertTrue(payload.has("total_minor"))
        assertEquals(1234, payload.getLong("total_minor"))
        assertEquals("food.dairy", payload.getJSONArray("items").getJSONObject(0).getString("category_slug"))
    }

    @Test
    fun createPayload_rejects_items_that_do_not_add_up() {
        val draft = FinanceDraft(
            type = FinanceTransactionType.EXPENSE,
            title = "Покупки",
            occurredOn = "2026-08-03",
            currency = "RUB",
            totalMinor = 1234,
            items = listOf(FinanceDraftItem("Молоко", 1.0, 120, "food.dairy")),
        )

        val exception = runCatching {
            FinanceJson.createTransactionPayload(draft, "account-1")
        }.exceptionOrNull()

        assertFalse(exception == null)
        assertEquals("Items must equal the transaction total.", exception?.message)
    }

    @Test
    fun parseMinorUnits_uses_decimal_not_binary_floating_point() {
        assertEquals(1234L, FinanceMoney.parseMinorUnits("12,34", "RUB"))
        assertEquals(10000L, FinanceMoney.parseMinorUnits("100", "RUB"))
    }

    @Test
    fun createPayload_rejects_missing_title() {
        val exception = runCatching {
            FinanceJson.createTransactionPayload(
                FinanceDraft(
                    FinanceTransactionType.EXPENSE,
                    " ",
                    "2026-08-03",
                    "RUB",
                    100,
                    listOf(FinanceDraftItem("Молоко", 1.0, 100, "food.dairy")),
                ),
                "account-1",
            )
        }.exceptionOrNull()

        assertEquals("Transaction title is required.", exception?.message)
    }
}
