package io.github.andivalsei.andivum

import android.content.Context
import android.net.Uri
import com.google.mlkit.vision.common.InputImage
import com.google.mlkit.vision.text.TextRecognition
import com.google.mlkit.vision.text.latin.TextRecognizerOptions

object ReceiptOcr {
    fun extract(
        context: Context,
        uri: Uri,
        onComplete: (Result<String>) -> Unit,
    ) {
        runCatching { InputImage.fromFilePath(context, uri) }
            .onSuccess { image ->
                val recognizer = TextRecognition.getClient(TextRecognizerOptions.DEFAULT_OPTIONS)
                recognizer.process(image)
                    .addOnSuccessListener { result -> onComplete(Result.success(result.text)) }
                    .addOnFailureListener { error -> onComplete(Result.failure(error)) }
                    .addOnCompleteListener { recognizer.close() }
            }
            .onFailure { error -> onComplete(Result.failure(error)) }
    }
}
