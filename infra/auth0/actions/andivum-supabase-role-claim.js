const ANDIVUM_CLIENT_IDS = new Set([
  "C3mYmpsm3g0Bs3e09bMyHTw0sXiTKCeV",
  "Gotbwwp9n3FNEv4TQ18KKYuKdxMGzSbO",
]);

exports.onExecutePostLogin = async (event, api) => {
  if (!ANDIVUM_CLIENT_IDS.has(event.client?.client_id)) {
    return;
  }

  // Supabase uses the literal role claim from the Auth0 ID token to select the
  // authenticated Postgres role. Do not add it to the access token: Auth0
  // strips non-namespaced access-token claims.
  api.idToken.setCustomClaim("role", "authenticated");
};
