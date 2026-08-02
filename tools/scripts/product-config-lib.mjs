export function validateProductConfiguration(product) {
  const productErrors = [];

  if (!product?.displayName?.trim()) {
    productErrors.push("displayName не может быть пустым");
  }

  if (!product?.supportedLocales?.includes(product.defaultLocale)) {
    productErrors.push("defaultLocale должен входить в supportedLocales");
  }

  if (!product?.supportedLocales?.includes(product.systemLocaleFallback)) {
    productErrors.push(
      "systemLocaleFallback должен входить в supportedLocales",
    );
  }

  for (const locale of ["en-US", "ru-RU"]) {
    if (!product?.supportedLocales?.includes(locale)) {
      productErrors.push(`На старте обязательна локаль ${locale}`);
    }
  }

  if (product?.theme?.default !== "system") {
    productErrors.push("Тема по умолчанию должна следовать системной");
  }

  return productErrors;
}
