package io.github.andivalsei.andivum

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Test

class FinanceTextImportTest {
    @Test
    fun parsesCsvWithDecimalComma() {
        val draft = FinanceTextImport.tryCreateDraft(
            "Хлеб;food.groceries;123,45",
            "receipt.csv",
            listOf(FinanceCategory("1", "food.groceries", "Продукты", "expense", null)),
        )

        assertNotNull(draft)
        assertEquals(12_345L, draft!!.totalMinor)
        assertEquals("food.groceries", draft.items.single().categorySlug)
    }
}
