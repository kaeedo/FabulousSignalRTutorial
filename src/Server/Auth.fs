module Auth

open Microsoft.AspNetCore.Authentication
open Microsoft.Extensions.Logging
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe

/// Authenticates the request against the given `schemes`.
/// If the request is authenticated, the authenticated identities are added to the current HTTP Context's User.
let authenticateMany (schemes: string seq): HttpHandler =
    fun next ctx -> task {
        for scheme in schemes do
            let logger = ctx.GetLogger(sprintf "Handlers.Authenticate.Scheme.%s" scheme)
            try
                let! authResult = ctx.AuthenticateAsync(scheme)
                if authResult.Succeeded
                then
                    ctx.User.AddIdentities(authResult.Principal.Identities) // augment other logins with our own
                    logger.LogInformation("Logged in user via scheme {0}", scheme)
            with
            | ex ->
                let a = ex
                ()
        
        return! next ctx
    }

/// Authenticates the request against the given `scheme`.
/// If the request is authenticated, the authenticated identities are added to the current HTTP Context's User.
let inline authenticate (scheme: string): HttpHandler = authenticateMany [scheme]

/// Authenticates the request against the given scheme and returns None if the authentication request failed
let tryAuthenticate (scheme: string): HttpHandler =
    fun next ctx -> task {
        let logger = ctx.GetLogger(sprintf "Handlers.Authenticate.Scheme.%s" scheme)
        try
        let! authResult = ctx.AuthenticateAsync(scheme)
        if authResult.Succeeded
        then
            ctx.User.AddIdentities(authResult.Principal.Identities) // augment other logins with our own
            logger.LogTrace("Logged in user via scheme {0}", scheme)
            return! next ctx
        else
            logger.LogTrace("Failed to log in user via scheme {0}", scheme)
            return None
        with
        | e ->
        logger.LogError(e, "Error while authenticating with auth scheme {name}", scheme)
        return None
    }

/// Validates if a user has successfully authenticated. This function checks if the auth middleware was able to establish a user's identity by validating certain parts of the HTTP request (e.g. a cookie or a token) and set the `User` object of the `HttpContext`.
/// This version is different from the built-in Giraffe version in that (and shadows it because) it checks all of the identities on the User, not just the first.
let requiresAuthentication authFailedHandler =
    authorizeUser (fun user -> isNotNull user && user.Identities |> Seq.exists (fun identity -> identity.IsAuthenticated)) authFailedHandler

/// Authenticates the request against the given `scheme`.
/// If the request is authenticated, the authenticated identities are added to the current HTTP Context's User.
/// If the reqeust is not authenticated, the request is terminated with a 401 status code.
///
/// This extends the built-in `requiresAuthentication` handler with the ability to authenticate against a particular scheme before doing the 'must be logged-in' check
let requiresAuthenticationScheme (scheme: string): HttpHandler = authenticate scheme >=> requiresAuthentication (setStatusCode 401)