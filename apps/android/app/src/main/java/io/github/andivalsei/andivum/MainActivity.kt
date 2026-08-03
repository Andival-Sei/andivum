package io.github.andivalsei.andivum

import android.net.Uri
import android.os.Bundle
import java.security.MessageDigest
import androidx.activity.result.contract.ActivityResultContracts
import androidx.activity.ComponentActivity
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.AccountBalanceWallet
import androidx.compose.material.icons.outlined.CheckCircleOutline
import androidx.compose.material.icons.outlined.Home
import androidx.compose.material.icons.outlined.Settings
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedCard
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation

private enum class DashboardTab {
    OVERVIEW,
    FINANCE,
}

private data class FinanceUiState(
    val categories: List<FinanceCategory> = emptyList(),
    val accounts: List<FinanceAccount> = emptyList(),
    val transactions: List<FinanceTransaction> = emptyList(),
    val isBusy: Boolean = false,
    val status: String? = null,
)

private data class FinanceFormItem(
    val name: String,
    val amount: String,
    val categorySlug: String,
)

class MainActivity : ComponentActivity() {
    private lateinit var authManager: AuthManager
    private lateinit var financeClient: FinanceClient
    private lateinit var financeSettings: SecureFinanceSettingsStore
    private var uiState by mutableStateOf(AuthShellState(isSignedIn = false))
    private var financeState by mutableStateOf(FinanceUiState())
    private var selectedDashboardTab by mutableStateOf(DashboardTab.OVERVIEW)
    private var pendingFinanceDraft by mutableStateOf<FinanceDraft?>(null)
    private var pendingFinanceSource by mutableStateOf("manual")
    private var pendingFinanceFingerprint by mutableStateOf<String?>(null)
    private var email by mutableStateOf("")
    private var password by mutableStateOf("")

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        authManager = AuthManager(this)
        val configuration = authManager.authConfiguration
        financeClient = FinanceClient(
            configuration.supabaseUrl,
            configuration.supabasePublishableKey,
            authManager::currentAccessToken,
        )
        financeSettings = SecureFinanceSettingsStore(this)
        val hasSavedSession = authManager.isSignedIn()
        uiState = AuthShellState(isSignedIn = false, isBusy = hasSavedSession)
        setContent {
            AndivumTheme {
                AndivumApp(
                    state = uiState,
                    email = email,
                    password = password,
                    onEmailChanged = { email = it },
                    onPasswordChanged = { password = it },
                    onSignIn = ::beginSignIn,
                    onSignUp = ::beginSignUp,
                    onSignOut = ::signOut,
                    onOpenAccountSettings = ::openAccountSettings,
                    selectedTab = selectedDashboardTab,
                    financeState = financeState,
                    pendingFinanceDraft = pendingFinanceDraft,
                    onSelectTab = ::selectDashboardTab,
                    onSaveFinance = ::saveFinance,
                    onImportFinance = ::importFinance,
                    onSaveGeminiKey = ::saveGeminiKey,
                    onDraftConsumed = { pendingFinanceDraft = null },
                )
            }
        }
        if (hasSavedSession) {
            authManager.validateSession { result ->
                runOnUiThread {
                    uiState = result.fold(
                        onSuccess = {
                            uiState.copy(
                                isSignedIn = true,
                                isBusy = false,
                                sessionStatus = getString(R.string.auth_session_verified),
                            )
                        },
                        onFailure = {
                            uiState.recoverAfterAuthFailure(
                                getString(R.string.auth_session_unavailable),
                            )
                        },
                    )
                    if (uiState.isSignedIn) loadFinance()
                }
            }
        }
    }

    override fun onDestroy() {
        authManager.close()
        super.onDestroy()
    }

    private fun beginSignIn(email: String, password: String) {
        if (uiState.isBusy) return
        uiState = uiState.copy(
            isBusy = true,
            message = null,
        )
        authManager.signIn(email, password) { result ->
            runOnUiThread {
                result.fold(
                    onSuccess = { operation ->
                        if (operation.sessionCreated) {
                            validateSession()
                        } else {
                            uiState = uiState.copy(
                                isBusy = false,
                                message = getString(R.string.auth_email_confirmation_required),
                            )
                        }
                    },
                    onFailure = { exception ->
                        uiState = uiState.recoverAfterAuthFailure(
                            exception.message ?: getString(R.string.auth_failed),
                        )
                    },
                )
            }
        }
    }

    private fun beginSignUp(email: String, password: String) {
        if (uiState.isBusy) return
        uiState = uiState.copy(
            isBusy = true,
            message = null,
        )
        authManager.signUp(email, password) { result ->
            runOnUiThread {
                result.fold(
                    onSuccess = { operation ->
                        if (operation.sessionCreated) {
                            validateSession()
                        } else {
                            uiState = uiState.copy(
                                isBusy = false,
                                message = getString(R.string.auth_email_confirmation_required),
                            )
                        }
                    },
                    onFailure = { exception ->
                        uiState = uiState.recoverAfterAuthFailure(
                            exception.message ?: getString(R.string.auth_failed),
                        )
                    },
                )
            }
        }
    }

    private fun validateSession() {
        authManager.validateSession { result ->
            runOnUiThread {
                uiState = result.fold(
                    onSuccess = {
                        uiState.copy(
                            isSignedIn = true,
                            isBusy = false,
                            message = null,
                            sessionStatus = getString(R.string.auth_session_verified),
                        )
                    },
                    onFailure = {
                        uiState.recoverAfterAuthFailure(
                            getString(R.string.auth_session_unavailable),
                        )
                    },
                )
                if (uiState.isSignedIn) loadFinance()
            }
        }
    }

    private fun beginSignIn() {
        beginSignIn(email, password)
    }

    private fun beginSignUp() {
        beginSignUp(email, password)
    }

    private fun signOut() {
        authManager.signOut()
        uiState = AuthShellState(isSignedIn = false)
        selectedDashboardTab = DashboardTab.OVERVIEW
        financeState = FinanceUiState()
        pendingFinanceDraft = null
        pendingFinanceSource = "manual"
        pendingFinanceFingerprint = null
    }

    private fun openAccountSettings() {
        uiState = uiState.copy(message = getString(R.string.auth_settings_unavailable))
    }

    private fun selectDashboardTab(tab: DashboardTab) {
        selectedDashboardTab = tab
        if (tab == DashboardTab.FINANCE && uiState.isSignedIn) loadFinance()
    }

    private fun loadFinance() {
        if (financeState.isBusy) return
        financeState = financeState.copy(isBusy = true, status = getString(R.string.finance_loading))
        Thread {
            runCatching {
                val categories = financeClient.getCategories()
                var accounts = financeClient.getAccounts()
                if (accounts.isEmpty()) {
                    accounts = listOf(financeClient.createAccount(getString(R.string.finance_default_account)))
                }
                val transactions = financeClient.getTransactions()
                Triple(categories, accounts, transactions)
            }.onSuccess { (categories, accounts, transactions) ->
                runOnUiThread {
                    financeState = FinanceUiState(
                        categories = categories,
                        accounts = accounts,
                        transactions = transactions,
                        status = if (transactions.isEmpty()) {
                            getString(R.string.finance_empty)
                        } else {
                            getString(R.string.finance_transaction_count, transactions.size)
                        },
                    )
                }
            }.onFailure { error ->
                runOnUiThread {
                    financeState = financeState.copy(
                        isBusy = false,
                        status = getString(R.string.finance_load_failed, error.message.orEmpty()),
                    )
                }
            }
        }.start()
    }

    private fun saveFinance(draft: FinanceDraft) {
        val accountId = financeState.accounts.firstOrNull()?.id
        if (accountId.isNullOrBlank()) {
            financeState = financeState.copy(status = getString(R.string.finance_account_required))
            return
        }
        financeState = financeState.copy(isBusy = true, status = getString(R.string.finance_saving))
        Thread {
            runCatching {
                financeClient.createTransaction(
                    draft,
                    accountId,
                    pendingFinanceSource,
                    pendingFinanceFingerprint,
                )
            }
                .onSuccess { result ->
                    runOnUiThread {
                        financeState = financeState.copy(
                            isBusy = false,
                            status = if (result.isDuplicate) {
                                getString(R.string.finance_duplicate)
                            } else {
                                getString(R.string.finance_saved)
                            },
                        )
                        loadFinance()
                    }
                }
                .onFailure { error ->
                    runOnUiThread {
                        financeState = financeState.copy(
                            isBusy = false,
                            status = error.message ?: getString(R.string.finance_save_failed),
                        )
                    }
                }
        }.start()
    }

    private fun saveGeminiKey(value: String) {
        runCatching { financeSettings.writeGeminiApiKey(value) }
            .onSuccess {
                financeState = financeState.copy(status = getString(R.string.finance_key_saved))
            }
            .onFailure { error -> financeState = financeState.copy(status = error.message) }
    }

    private fun importFinance(uri: Uri) {
        if (financeState.isBusy) return
        val mimeType = contentResolver.getType(uri).orEmpty().ifBlank { "application/octet-stream" }
        financeState = financeState.copy(isBusy = true, status = getString(R.string.finance_importing))
        val bytes = runCatching {
            contentResolver.openInputStream(uri)?.use { it.readBytes() }
                ?: throw IllegalStateException("Cannot read selected document.")
        }.getOrElse { error ->
            financeState = financeState.copy(isBusy = false, status = error.message)
            return
        }
        if (bytes.isEmpty() || bytes.size > 20 * 1024 * 1024) {
            financeState = financeState.copy(
                isBusy = false,
                status = getString(R.string.finance_file_size_invalid),
            )
            return
        }
        if (!hasSupportedSignature(bytes, mimeType)) {
            financeState = financeState.copy(
                isBusy = false,
                status = getString(R.string.finance_file_type_invalid),
            )
            return
        }
        val fingerprint = sha256(bytes)
        val apiKey = financeSettings.readGeminiApiKey()
        if (!apiKey.isNullOrBlank()) {
            Thread {
                runCatching {
                    GeminiReceiptParser.parse(bytes, mimeType, financeState.categories, apiKey)
                }.onSuccess { draft ->
                    runOnUiThread {
                        pendingFinanceDraft = draft
                        pendingFinanceSource = "ai"
                        pendingFinanceFingerprint = fingerprint
                        financeState = financeState.copy(isBusy = false, status = getString(R.string.finance_draft_ready))
                    }
                }.onFailure { error ->
                    runOnUiThread { financeState = financeState.copy(isBusy = false, status = error.message) }
                }
            }.start()
            return
        }
        if (mimeType.startsWith("image/")) {
            ReceiptOcr.extract(this, uri) { result ->
                runOnUiThread {
                    val draft = result.getOrNull()?.let {
                        FinanceTextImport.tryCreateDraft(it, "receipt.txt", financeState.categories)
                    }
                    pendingFinanceDraft = draft
                    if (draft != null) {
                        pendingFinanceSource = "ocr"
                        pendingFinanceFingerprint = fingerprint
                    }
                    financeState = financeState.copy(
                        isBusy = false,
                        status = if (draft == null) getString(R.string.finance_ocr_manual_review) else getString(R.string.finance_draft_ready),
                    )
                }
            }
            return
        }
        if (mimeType.startsWith("text/") ||
            mimeType == "message/rfc822" ||
            mimeType.contains("ofx") ||
            mimeType.contains("qfx") ||
            mimeType == "application/vnd.intu.qbo") {
            val draft = FinanceTextImport.tryCreateDraft(
                bytes.toString(Charsets.UTF_8),
                "import.txt",
                financeState.categories,
            )
            pendingFinanceDraft = draft
            if (draft != null) {
                pendingFinanceSource = "import"
                pendingFinanceFingerprint = fingerprint
            }
            financeState = financeState.copy(
                isBusy = false,
                status = if (draft == null) getString(R.string.finance_text_manual_review) else getString(R.string.finance_draft_ready),
            )
            return
        }
        financeState = financeState.copy(
            isBusy = false,
            status = getString(R.string.finance_key_required_for_document),
        )
    }

    private fun sha256(bytes: ByteArray): String = MessageDigest
        .getInstance("SHA-256")
        .digest(bytes)
        .joinToString("") { byte -> "%02x".format(byte) }

    private fun hasSupportedSignature(bytes: ByteArray, mimeType: String): Boolean {
        if (mimeType.startsWith("text/") ||
            mimeType == "message/rfc822" ||
            mimeType.contains("ofx") ||
            mimeType.contains("qfx") ||
            mimeType == "application/vnd.intu.qbo") {
            return true
        }
        if (mimeType == "application/pdf") {
            return bytes.size >= 5 && bytes.copyOfRange(0, 5).toString(Charsets.US_ASCII) == "%PDF-"
        }
        if (mimeType == "image/jpeg") {
            return bytes.size >= 3 && bytes[0] == 0xFF.toByte() && bytes[1] == 0xD8.toByte() && bytes[2] == 0xFF.toByte()
        }
        if (mimeType == "image/png") {
            return bytes.size >= 8 && bytes.copyOfRange(0, 8).contentEquals(
                byteArrayOf(0x89.toByte(), 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A),
            )
        }
        if (mimeType == "image/webp") {
            return bytes.size >= 12 &&
                bytes.copyOfRange(0, 4).toString(Charsets.US_ASCII) == "RIFF" &&
                bytes.copyOfRange(8, 12).toString(Charsets.US_ASCII) == "WEBP"
        }
        return mimeType == "image/heic" && bytes.size >= 12 &&
            bytes.copyOfRange(4, 8).toString(Charsets.US_ASCII) == "ftyp"
    }
}

@Composable
private fun FinanceScreen(
    modifier: Modifier,
    innerPadding: PaddingValues,
    state: FinanceUiState,
    pendingDraft: FinanceDraft?,
    onDraftConsumed: () -> Unit,
    onSave: (FinanceDraft) -> Unit,
    onImport: (Uri) -> Unit,
    onSaveGeminiKey: (String) -> Unit,
) {
    val picker = rememberLauncherForActivityResult(
        ActivityResultContracts.OpenDocument(),
    ) { uri -> uri?.let(onImport) }
    val defaultItemName = stringResource(R.string.finance_default_item)
    val defaultTitle = stringResource(R.string.finance_default_title)
    var title by remember { mutableStateOf(defaultTitle) }
    var total by remember { mutableStateOf("") }
    var occurredOn by remember { mutableStateOf(java.time.LocalDate.now().toString()) }
    var transactionType by remember { mutableStateOf(FinanceTransactionType.EXPENSE) }
    var selectedAccountId by remember { mutableStateOf("") }
    var typeMenuExpanded by remember { mutableStateOf(false) }
    var accountMenuExpanded by remember { mutableStateOf(false) }
    var apiKey by remember { mutableStateOf("") }
    var formError by remember { mutableStateOf<String?>(null) }
    val formItems = remember {
        mutableStateListOf(FinanceFormItem(defaultItemName, "", "other.expense"))
    }

    LaunchedEffect(state.accounts) {
        if (selectedAccountId.isBlank()) selectedAccountId = state.accounts.firstOrNull()?.id.orEmpty()
    }
    LaunchedEffect(pendingDraft) {
        pendingDraft?.let { draft ->
            title = draft.title
            total = draft.totalMinor.toMajorAmount(draft.currency)
            occurredOn = draft.occurredOn
            transactionType = draft.type
            formItems.clear()
            formItems.addAll(draft.items.map { item ->
                FinanceFormItem(
                    item.name,
                    item.lineTotalMinor.toMajorAmount(draft.currency),
                    item.categorySlug,
                )
            })
            onDraftConsumed()
        }
    }

    val account = state.accounts.firstOrNull { it.id == selectedAccountId }
    val currency = account?.currency ?: "RUB"
    val categoryType = transactionType.name.lowercase()
    val availableCategories = state.categories.filter { it.type == categoryType }
    val newItemLabel = stringResource(R.string.finance_new_item)
    val itemsTotalMismatchLabel = stringResource(R.string.finance_items_total_mismatch)
    val formInvalidLabel = stringResource(R.string.finance_form_invalid)

    LaunchedEffect(transactionType, state.categories) {
        val defaultCategory = if (transactionType == FinanceTransactionType.INCOME) {
            "income.other"
        } else {
            "other.expense"
        }
        formItems.indices.forEach { index ->
            if (availableCategories.none { it.slug == formItems[index].categorySlug }) {
                formItems[index] = formItems[index].copy(categorySlug = defaultCategory)
            }
        }
    }

    LazyColumn(
        modifier = modifier,
        contentPadding = PaddingValues(
            start = 20.dp,
            end = 20.dp,
            top = innerPadding.calculateTopPadding() + 16.dp,
            bottom = innerPadding.calculateBottomPadding() + 20.dp,
        ),
        verticalArrangement = Arrangement.spacedBy(14.dp),
    ) {
        item {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(10.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        stringResource(R.string.finance_eyebrow),
                        style = MaterialTheme.typography.labelLarge,
                        color = MaterialTheme.colorScheme.primary,
                    )
                    Text(
                        stringResource(R.string.finance_title),
                        style = MaterialTheme.typography.headlineMedium,
                        fontWeight = FontWeight.Bold,
                    )
                }
                Button(
                    onClick = {
                        picker.launch(
                            arrayOf(
                                "image/*",
                                "application/pdf",
                                "text/*",
                                "message/rfc822",
                                "application/x-ofx",
                                "application/ofx",
                                "application/qfx",
                                "application/vnd.intu.qbo",
                            ),
                        )
                    },
                    enabled = !state.isBusy,
                ) {
                    Text(stringResource(R.string.finance_import))
                }
            }
        }
        item {
            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(24.dp),
            ) {
                Column(
                    modifier = Modifier.padding(20.dp),
                    verticalArrangement = Arrangement.spacedBy(12.dp),
                ) {
                    Text(
                        stringResource(R.string.finance_new_transaction),
                        style = MaterialTheme.typography.titleLarge,
                        fontWeight = FontWeight.SemiBold,
                    )
                    OutlinedTextField(
                        value = title,
                        onValueChange = { title = it },
                        modifier = Modifier.fillMaxWidth(),
                        label = { Text(stringResource(R.string.finance_name)) },
                        singleLine = true,
                    )
                    Box {
                        Button(onClick = { typeMenuExpanded = true }) {
                            Text(
                                if (transactionType == FinanceTransactionType.INCOME) {
                                    stringResource(R.string.finance_income)
                                } else {
                                    stringResource(R.string.finance_expense)
                                },
                            )
                        }
                        DropdownMenu(
                            expanded = typeMenuExpanded,
                            onDismissRequest = { typeMenuExpanded = false },
                        ) {
                            DropdownMenuItem(
                                text = { Text(stringResource(R.string.finance_expense)) },
                                onClick = {
                                    transactionType = FinanceTransactionType.EXPENSE
                                    typeMenuExpanded = false
                                },
                            )
                            DropdownMenuItem(
                                text = { Text(stringResource(R.string.finance_income)) },
                                onClick = {
                                    transactionType = FinanceTransactionType.INCOME
                                    typeMenuExpanded = false
                                },
                            )
                        }
                    }
                    OutlinedTextField(
                        value = total,
                        onValueChange = { total = it },
                        modifier = Modifier.fillMaxWidth(),
                        label = { Text(stringResource(R.string.finance_total, currency)) },
                        singleLine = true,
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
                    )
                    OutlinedTextField(
                        value = occurredOn,
                        onValueChange = { occurredOn = it },
                        modifier = Modifier.fillMaxWidth(),
                        label = { Text(stringResource(R.string.finance_date)) },
                        singleLine = true,
                    )
                    Box {
                        Button(onClick = { accountMenuExpanded = true }) {
                            Text(account?.name ?: stringResource(R.string.finance_account_required))
                        }
                        DropdownMenu(
                            expanded = accountMenuExpanded,
                            onDismissRequest = { accountMenuExpanded = false },
                        ) {
                            state.accounts.forEach { candidate ->
                                DropdownMenuItem(
                                    text = { Text("${candidate.name} (${candidate.currency})") },
                                    onClick = {
                                        selectedAccountId = candidate.id
                                        accountMenuExpanded = false
                                    },
                                )
                            }
                        }
                    }
                }
            }
        }
        item {
            Text(
                stringResource(R.string.finance_items),
                style = MaterialTheme.typography.titleLarge,
                fontWeight = FontWeight.SemiBold,
            )
        }
        itemsIndexed(formItems) { index, item ->
            var categoryMenuExpanded by remember { mutableStateOf(false) }
            OutlinedCard(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(18.dp),
            ) {
                Column(
                    modifier = Modifier.padding(14.dp),
                    verticalArrangement = Arrangement.spacedBy(10.dp),
                ) {
                    OutlinedTextField(
                        value = item.name,
                        onValueChange = { formItems[index] = item.copy(name = it) },
                        modifier = Modifier.fillMaxWidth(),
                        label = { Text(stringResource(R.string.finance_item_name)) },
                        singleLine = true,
                    )
                    OutlinedTextField(
                        value = item.amount,
                        onValueChange = { formItems[index] = item.copy(amount = it) },
                        modifier = Modifier.fillMaxWidth(),
                        label = { Text(stringResource(R.string.finance_item_amount, currency)) },
                        singleLine = true,
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
                    )
                    Box {
                        Button(onClick = { categoryMenuExpanded = true }) {
                            Text(
                                availableCategories.firstOrNull { it.slug == item.categorySlug }?.name
                                    ?: item.categorySlug,
                            )
                        }
                        DropdownMenu(
                            expanded = categoryMenuExpanded,
                            onDismissRequest = { categoryMenuExpanded = false },
                        ) {
                            availableCategories.forEach { category ->
                                DropdownMenuItem(
                                    text = { Text(category.name) },
                                    onClick = {
                                        formItems[index] = item.copy(categorySlug = category.slug)
                                        categoryMenuExpanded = false
                                    },
                                )
                            }
                        }
                    }
                }
            }
        }
        item {
            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                TextButton(
                    onClick = {
                        formItems.add(
                            FinanceFormItem(
                                newItemLabel,
                                "",
                                if (transactionType == FinanceTransactionType.INCOME) "income.other" else "other.expense",
                            ),
                        )
                    },
                ) {
                    Text(stringResource(R.string.finance_add_item))
                }
                Button(
                    onClick = {
                        formError = runCatching {
                            val parsedItems = formItems.map { item ->
                                FinanceDraftItem(
                                    item.name,
                                    1.0,
                                    FinanceMoney.parseMinorUnits(item.amount, currency),
                                    item.categorySlug,
                                )
                            }
                            val parsedTotal = FinanceMoney.parseMinorUnits(total, currency)
                            require(parsedItems.isNotEmpty() && parsedItems.sumOf { it.lineTotalMinor } == parsedTotal) {
                                itemsTotalMismatchLabel
                            }
                            FinanceDraft(
                                transactionType,
                                title,
                                occurredOn,
                                currency,
                                parsedTotal,
                                parsedItems,
                            )
                        }.fold(
                            onSuccess = { onSave(it); null },
                            onFailure = { it.message ?: formInvalidLabel },
                        )
                    },
                    enabled = !state.isBusy,
                ) {
                    Text(stringResource(R.string.finance_save))
                }
            }
        }
        formError?.let { error ->
            item {
                Text(error, color = MaterialTheme.colorScheme.error)
            }
        }
        state.status?.let { status ->
            item {
                Text(status, color = MaterialTheme.colorScheme.primary)
            }
        }
        item {
            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(20.dp),
            ) {
                Column(
                    modifier = Modifier.padding(18.dp),
                    verticalArrangement = Arrangement.spacedBy(10.dp),
                ) {
                    Text(
                        stringResource(R.string.finance_ai_settings),
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.SemiBold,
                    )
                    Text(
                        stringResource(R.string.finance_ai_description),
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    OutlinedTextField(
                        value = apiKey,
                        onValueChange = { apiKey = it },
                        modifier = Modifier.fillMaxWidth(),
                        label = { Text(stringResource(R.string.finance_gemini_key)) },
                        singleLine = true,
                        visualTransformation = PasswordVisualTransformation(),
                    )
                    TextButton(onClick = { onSaveGeminiKey(apiKey); apiKey = "" }) {
                        Text(stringResource(R.string.finance_save_key))
                    }
                }
            }
        }
        item {
            Text(
                stringResource(R.string.finance_recent),
                style = MaterialTheme.typography.titleLarge,
                fontWeight = FontWeight.SemiBold,
            )
        }
        items(state.transactions) { transaction ->
            OutlinedCard(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(16.dp)) {
                Row(
                    modifier = Modifier.padding(16.dp).fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(12.dp),
                ) {
                    Column(modifier = Modifier.weight(1f)) {
                        Text(transaction.title, fontWeight = FontWeight.SemiBold)
                        Text(transaction.occurredOn, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                    Text(
                        transaction.totalMinor.toMajorAmount(transaction.currency) + " " + transaction.currency,
                        fontWeight = FontWeight.SemiBold,
                    )
                }
            }
        }
    }
}

private fun Long.toMajorAmount(currency: String): String =
    (this / if (currency == "JPY" || currency == "KRW") 1.0 else 100.0).toString()

@Composable
private fun AndivumApp(
    state: AuthShellState,
    email: String,
    password: String,
    onEmailChanged: (String) -> Unit,
    onPasswordChanged: (String) -> Unit,
    onSignIn: () -> Unit,
    onSignUp: () -> Unit,
    onSignOut: () -> Unit,
    onOpenAccountSettings: () -> Unit,
    selectedTab: DashboardTab,
    financeState: FinanceUiState,
    pendingFinanceDraft: FinanceDraft?,
    onSelectTab: (DashboardTab) -> Unit,
    onSaveFinance: (FinanceDraft) -> Unit,
    onImportFinance: (Uri) -> Unit,
    onSaveGeminiKey: (String) -> Unit,
    onDraftConsumed: () -> Unit,
) {
    when (state.screen) {
        AuthShellScreen.SIGN_IN -> SignInScreen(
            state = state,
            email = email,
            password = password,
            onEmailChanged = onEmailChanged,
            onPasswordChanged = onPasswordChanged,
            onSignIn = onSignIn,
            onSignUp = onSignUp,
        )
        AuthShellScreen.DASHBOARD -> DashboardScreen(
            sessionStatus = state.sessionStatus,
            onSignOut = onSignOut,
            onOpenAccountSettings = onOpenAccountSettings,
            selectedTab = selectedTab,
            financeState = financeState,
            pendingFinanceDraft = pendingFinanceDraft,
            onSelectTab = onSelectTab,
            onSaveFinance = onSaveFinance,
            onImportFinance = onImportFinance,
            onSaveGeminiKey = onSaveGeminiKey,
            onDraftConsumed = onDraftConsumed,
        )
    }
}

@Composable
private fun SignInScreen(
    state: AuthShellState,
    email: String,
    password: String,
    onEmailChanged: (String) -> Unit,
    onPasswordChanged: (String) -> Unit,
    onSignIn: () -> Unit,
    onSignUp: () -> Unit,
) {
    Surface(modifier = Modifier.fillMaxSize()) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 24.dp, vertical = 32.dp),
            verticalArrangement = Arrangement.spacedBy(20.dp),
        ) {
            Spacer(modifier = Modifier.height(12.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                Surface(
                    modifier = Modifier.size(52.dp),
                    shape = RoundedCornerShape(18.dp),
                    color = MaterialTheme.colorScheme.primary,
                ) {
                    Box(contentAlignment = Alignment.Center) {
                        Text(
                            text = "A",
                            color = MaterialTheme.colorScheme.onPrimary,
                            style = MaterialTheme.typography.headlineSmall,
                            fontWeight = FontWeight.Bold,
                        )
                    }
                }
                Spacer(modifier = Modifier.width(14.dp))
                Column {
                    Text(
                        text = stringResource(R.string.app_name),
                        style = MaterialTheme.typography.titleLarge,
                        fontWeight = FontWeight.SemiBold,
                    )
                    Text(
                        text = stringResource(R.string.auth_eyebrow),
                        style = MaterialTheme.typography.labelMedium,
                        color = MaterialTheme.colorScheme.primary,
                    )
                }
            }

            Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                Text(
                    text = stringResource(R.string.auth_title),
                    style = MaterialTheme.typography.headlineLarge,
                    fontWeight = FontWeight.Bold,
                )
                Text(
                    text = stringResource(R.string.auth_subtitle),
                    style = MaterialTheme.typography.bodyLarge,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }

            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(28.dp),
                colors = CardDefaults.cardColors(
                    containerColor = MaterialTheme.colorScheme.primaryContainer,
                ),
            ) {
                Column(
                    modifier = Modifier.padding(24.dp),
                    verticalArrangement = Arrangement.spacedBy(14.dp),
                ) {
                    Text(
                        text = stringResource(R.string.auth_card_title),
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.SemiBold,
                    )
                    Text(
                        text = stringResource(R.string.auth_card_body),
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onPrimaryContainer,
                    )
                    OutlinedTextField(
                        value = email,
                        onValueChange = onEmailChanged,
                        enabled = !state.isBusy,
                        modifier = Modifier.fillMaxWidth(),
                        label = { Text(stringResource(R.string.auth_email_label)) },
                        singleLine = true,
                        keyboardOptions = KeyboardOptions(
                            keyboardType = KeyboardType.Email,
                        ),
                    )
                    OutlinedTextField(
                        value = password,
                        onValueChange = onPasswordChanged,
                        enabled = !state.isBusy,
                        modifier = Modifier.fillMaxWidth(),
                        label = { Text(stringResource(R.string.auth_password_label)) },
                        singleLine = true,
                        visualTransformation = PasswordVisualTransformation(),
                        keyboardOptions = KeyboardOptions(
                            keyboardType = KeyboardType.Password,
                        ),
                    )
                    Button(
                        onClick = onSignIn,
                        enabled = !state.isBusy,
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(56.dp),
                        shape = RoundedCornerShape(18.dp),
                    ) {
                        Text(
                            text = stringResource(
                                if (state.isBusy) R.string.auth_working else R.string.sign_in,
                            ),
                            fontWeight = FontWeight.SemiBold,
                        )
                    }
                    TextButton(
                        onClick = onSignUp,
                        enabled = !state.isBusy,
                        modifier = Modifier.fillMaxWidth(),
                    ) {
                        Text(text = stringResource(R.string.sign_up))
                    }
                }
            }

            state.message?.let { message ->
                Text(
                    text = message,
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.error,
                )
            }

            Text(
                text = stringResource(R.string.auth_security_note),
                modifier = Modifier.fillMaxWidth(),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                textAlign = TextAlign.Center,
            )
        }
    }
}

private data class ModulePlaceholder(
    val title: Int,
    val body: Int,
    val status: Int,
    val icon: @Composable () -> Unit,
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun DashboardScreen(
    sessionStatus: String?,
    onSignOut: () -> Unit,
    onOpenAccountSettings: () -> Unit,
    selectedTab: DashboardTab,
    financeState: FinanceUiState,
    pendingFinanceDraft: FinanceDraft?,
    onSelectTab: (DashboardTab) -> Unit,
    onSaveFinance: (FinanceDraft) -> Unit,
    onImportFinance: (Uri) -> Unit,
    onSaveGeminiKey: (String) -> Unit,
    onDraftConsumed: () -> Unit,
) {
    val modules = listOf(
        ModulePlaceholder(
            title = R.string.module_tasks_title,
            body = R.string.module_tasks_body,
            status = R.string.module_coming_soon,
            icon = {
                Icon(
                    imageVector = Icons.Outlined.CheckCircleOutline,
                    contentDescription = null,
                )
            },
        ),
        ModulePlaceholder(
            title = R.string.module_finance_title,
            body = R.string.module_finance_body,
            status = R.string.module_coming_soon,
            icon = {
                Icon(
                    imageVector = Icons.Outlined.AccountBalanceWallet,
                    contentDescription = null,
                )
            },
        ),
    )

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Column {
                        Text(
                            text = stringResource(R.string.app_name),
                            style = MaterialTheme.typography.titleLarge,
                        )
                        Text(
                            text = stringResource(
                                if (selectedTab == DashboardTab.FINANCE) R.string.navigation_finance
                                else R.string.dashboard_overview,
                            ),
                            style = MaterialTheme.typography.labelMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                },
                actions = {
                    TextButton(onClick = onSignOut) {
                        Text(stringResource(R.string.sign_out))
                    }
                },
            )
        },
        bottomBar = {
            NavigationBar {
                NavigationBarItem(
                    selected = selectedTab == DashboardTab.OVERVIEW,
                    onClick = { onSelectTab(DashboardTab.OVERVIEW) },
                    icon = { Icon(Icons.Outlined.Home, contentDescription = null) },
                    label = { Text(stringResource(R.string.navigation_overview)) },
                )
                NavigationBarItem(
                    selected = selectedTab == DashboardTab.FINANCE,
                    onClick = { onSelectTab(DashboardTab.FINANCE) },
                    icon = { Icon(Icons.Outlined.AccountBalanceWallet, contentDescription = null) },
                    label = { Text(stringResource(R.string.navigation_finance)) },
                )
                NavigationBarItem(
                    selected = false,
                    onClick = onOpenAccountSettings,
                    icon = { Icon(Icons.Outlined.Settings, contentDescription = null) },
                    label = { Text(stringResource(R.string.navigation_more)) },
                )
            }
        },
    ) { innerPadding ->
        if (selectedTab == DashboardTab.FINANCE) {
            FinanceScreen(
                modifier = Modifier.fillMaxSize(),
                innerPadding = innerPadding,
                state = financeState,
                pendingDraft = pendingFinanceDraft,
                onDraftConsumed = onDraftConsumed,
                onSave = onSaveFinance,
                onImport = onImportFinance,
                onSaveGeminiKey = onSaveGeminiKey,
            )
        } else {
            LazyColumn(
            modifier = Modifier.fillMaxSize(),
            contentPadding = PaddingValues(
                start = 20.dp,
                end = 20.dp,
                top = innerPadding.calculateTopPadding() + 16.dp,
                bottom = innerPadding.calculateBottomPadding() + 20.dp,
            ),
            verticalArrangement = Arrangement.spacedBy(16.dp),
        ) {
            item {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(28.dp),
                    colors = CardDefaults.cardColors(
                        containerColor = MaterialTheme.colorScheme.primaryContainer,
                    ),
                ) {
                    Column(
                        modifier = Modifier.padding(24.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp),
                    ) {
                        Text(
                            text = stringResource(R.string.dashboard_eyebrow),
                            style = MaterialTheme.typography.labelLarge,
                            color = MaterialTheme.colorScheme.primary,
                        )
                        Text(
                            text = stringResource(R.string.dashboard_title),
                            style = MaterialTheme.typography.headlineSmall,
                            fontWeight = FontWeight.Bold,
                        )
                        Text(
                            text = stringResource(R.string.dashboard_body),
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onPrimaryContainer,
                        )
                        sessionStatus?.let { status ->
                            Text(
                                text = status,
                                style = MaterialTheme.typography.labelLarge,
                                color = MaterialTheme.colorScheme.primary,
                            )
                        }
                    }
                }
            }
            item {
                Text(
                    text = stringResource(R.string.dashboard_modules),
                    style = MaterialTheme.typography.titleLarge,
                    fontWeight = FontWeight.SemiBold,
                )
            }
            items(modules) { module ->
                OutlinedCard(
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(22.dp),
                ) {
                    Row(
                        modifier = Modifier.padding(20.dp),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        Surface(
                            modifier = Modifier.size(48.dp),
                            shape = RoundedCornerShape(16.dp),
                            color = MaterialTheme.colorScheme.secondaryContainer,
                        ) {
                            Box(
                                contentAlignment = Alignment.Center,
                            ) {
                                module.icon()
                            }
                        }
                        Spacer(modifier = Modifier.width(16.dp))
                        Column(
                            modifier = Modifier.weight(1f),
                            verticalArrangement = Arrangement.spacedBy(4.dp),
                        ) {
                            Text(
                                text = stringResource(module.title),
                                style = MaterialTheme.typography.titleMedium,
                                fontWeight = FontWeight.SemiBold,
                            )
                            Text(
                                text = stringResource(module.body),
                                style = MaterialTheme.typography.bodyMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                        }
                        Text(
                            text = stringResource(module.status),
                            style = MaterialTheme.typography.labelMedium,
                            color = MaterialTheme.colorScheme.primary,
                        )
                    }
                }
            }
            item {
                Text(
                    text = stringResource(R.string.dashboard_footer),
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(top = 4.dp),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    textAlign = TextAlign.Center,
                )
            }
            }
        }
    }
}
