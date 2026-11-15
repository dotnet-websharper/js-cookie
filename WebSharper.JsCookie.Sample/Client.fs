namespace WebSharper.JsCookie.Sample

open WebSharper
open WebSharper.JavaScript
open WebSharper.UI
open WebSharper.UI.Client
open WebSharper.UI.Html
open WebSharper.UI.Templating
open WebSharper.JsCookie

[<JavaScript>]
module Client =

    // The template is the whole document, loaded from the DOM, so editing
    // index.html and refreshing the browser is enough.
    type IndexTemplate = Template<"index.html", ClientLoad.FromDocument>

    // ------------------------------------------------------------------
    // State bound into the template
    // ------------------------------------------------------------------

    // Theme: light is the default; the choice is persisted in localStorage.
    // The storage key is shared with the bootstrap script in index.html.
    let themeKey = "ws-jscookie-theme"
    let theme = Var.Create "light"
    let themeLabel = Var.Create "🌙 Dark mode"

    // The "set a cookie" form.
    let cookieName = Var.Create "ws-demo"
    let cookieValue = Var.Create "hello from WebSharper"
    let attrPath = Var.Create "/"
    let attrDomain = Var.Create ""
    let attrExpires = Var.Create "7"
    let attrSameSite = Var.Create "lax"
    let attrSecure = Var.Create false

    // The "read a cookie" form.
    let readName = Var.Create "ws-demo"

    // Readouts.
    let status = Var.Create "Ready — set a cookie to get started."
    let lastResult = Var.Create "No API call yet."
    let rawDocumentCookie = Var.Create "-"
    let attributesText = Var.Create "-"
    let converterText = Var.Create "-"
    let jarCount = Var.Create "0"
    let cookies = Var.Create ([] : (string * string) list)

    let applyTheme () =
        JS.Document.DocumentElement.SetAttribute("data-theme", theme.Value)
        themeLabel.Value <- if theme.Value = "dark" then "☀️ Light mode" else "🌙 Dark mode"

    let toggleTheme () =
        theme.Value <- (if theme.Value = "dark" then "light" else "dark")
        try
            JS.Window.LocalStorage.SetItem(themeKey, theme.Value)
        with _ ->
            ()
        applyTheme ()

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    // `CookieAttributes.Expires`/`SameSite` are WIG unions, so stringify the
    // runtime value with the JS `String` global rather than F#'s `string`.
    [<Inline "String($0)">]
    let private jsString (o: obj) : string = X<string>

    let expiresDays (s: string) =
        let v = JS.ParseFloat s
        if JS.IsNaN(v) then None else Some v

    /// Reads the form into a fresh `CookieAttributes` object.
    let currentAttributes () =
        let a = CookieAttributes()
        if attrPath.Value <> "" then a.Path <- attrPath.Value
        if attrDomain.Value <> "" then a.Domain <- attrDomain.Value
        match expiresDays attrExpires.Value with
        | Some days -> a.Expires <- days
        | None -> ()
        if attrSecure.Value then a.Secure <- true
        match attrSameSite.Value with
        | "strict" -> a.SameSite <- SameSite.Strict
        | "lax" -> a.SameSite <- SameSite.Lax
        | "none" -> a.SameSite <- SameSite.None
        | _ -> ()
        a

    let describeAttributes (a: CookieAttributes) =
        let orUnset (s: string) =
            if s = "" || s = "undefined" || s = "null" then "(unset)" else s
        sprintf "path=%s · domain=%s · secure=%s · expires=%s · sameSite=%s"
            (if isNull a.Path then "(default /)" else a.Path)
            (if isNull a.Domain then "(current host)" else a.Domain)
            (orUnset (jsString (box a.Secure)))
            (orUnset (jsString a.Expires))
            (orUnset (jsString a.SameSite))

    /// Rebuilds the cookie jar table and the loose readouts.
    let refresh () =
        let jar = Cookies.Get()
        let keys = JavaScript.Object.Keys(As<obj> jar)
        cookies.Value <- [ for k in keys -> (k, jar.[k]) ]
        jarCount.Value <- string keys.Length
        rawDocumentCookie.Value <-
            if isNull JS.Document.Cookie || JS.Document.Cookie = "" then "(empty)"
            else JS.Document.Cookie
        let a = Cookies.Attributes
        attributesText.Value <- describeAttributes a
        let c = Cookies.Converter
        converterText.Value <-
            sprintf "read=%s · write=%s"
                (if isNull c.Read then "(none)" else "set")
                (if isNull c.Write then "(none)" else "set")

    // ------------------------------------------------------------------
    // Actions — the small, per-call API
    // ------------------------------------------------------------------

    let save () =
        let name = cookieName.Value
        if name = "" then
            status.Value <- "A cookie name is required."
        else
            let written = Cookies.Set(name, cookieValue.Value, currentAttributes ())
            lastResult.Value <-
                if isNull written then
                    "Cookies.Set returned undefined — the browser rejected the cookie."
                else
                    "Cookies.Set wrote: " + written
            status.Value <- "Set the cookie \"" + name + "\"."
            refresh ()

    let read () =
        let name = readName.Value
        let value = Cookies.Get(name)
        lastResult.Value <-
            if isNull value then "Cookies.Get(\"" + name + "\") -> undefined"
            else "Cookies.Get(\"" + name + "\") -> " + value
        status.Value <- "Read the cookie \"" + name + "\"."

    let readAll () =
        let jar = Cookies.Get()
        let keys = JavaScript.Object.Keys(As<obj> jar)
        lastResult.Value <-
            if keys.Length = 0 then "Cookies.Get() -> {} (the jar is empty)"
            else
                keys
                |> Array.map (fun k -> k + " = " + jar.[k])
                |> String.concat "\n"
                |> fun s -> "Cookies.Get() ->\n" + s
        status.Value <- sprintf "Read all %d cookie(s)." keys.Length
        refresh ()

    let remove () =
        let name = readName.Value
        Cookies.Remove(name, currentAttributes ())
        status.Value <- "Removed the cookie \"" + name + "\"."
        lastResult.Value <- "Cookies.Remove(\"" + name + "\")."
        refresh ()

    // ------------------------------------------------------------------
    // Actions — the derived APIs
    // ------------------------------------------------------------------

    let scopedSet () =
        // A derived instance whose defaults include a strict SameSite.
        let scoped =
            Cookies.WithAttributes(CookieAttributes(Path = "/", SameSite = SameSite.Strict))
        let written = scoped.Set("ws-scoped", "set through withAttributes")
        let back = scoped.Get("ws-scoped")
        lastResult.Value <-
            sprintf "WithAttributes(...).Set -> %s\nWithAttributes(...).Get -> %s"
                (if isNull written then "undefined" else written)
                (if isNull back then "undefined" else back)
        status.Value <- "Set and read a cookie through a WithAttributes instance."
        refresh ()

    let converted () =
        // A converter that tags values on write and strips the tag on read.
        let tag = "ws|"
        let converter = Converter()
        converter.Read <- fun value _name -> value.Replace(tag, "")
        converter.Write <- fun value _name -> tag + value
        let convertedApi = Cookies.WithConverter(converter)
        convertedApi.Set("ws-converted", "raw value") |> ignore
        let back = convertedApi.Get("ws-converted")
        lastResult.Value <-
            sprintf "WithConverter(...).Get -> %s\nThe cookie is stored tagged; Get returns the untagged value."
                (if isNull back then "undefined" else back)
        status.Value <- "Round-tripped a value through a WithConverter instance."
        refresh ()

    let noConflict () =
        // js-cookie's `noConflict` only exists in the UMD/global build of the
        // package, not in the ES module a bundler resolves, so the binding does
        // not expose it. Probe the runtime to show the distinction safely.
        lastResult.Value <-
            if JS.Global?Cookies ===. JS.Undefined then
                "globalThis.Cookies is undefined — nothing to conflict with under a bundler."
            else
                "globalThis.Cookies exists; the module build does not expose noConflict."
        status.Value <- "Checked for a global Cookies conflict."

    // ------------------------------------------------------------------
    // Entry point
    // ------------------------------------------------------------------
    [<SPAEntryPoint>]
    let Main () =
        // Restore the theme before the template is bound, so the first paint
        // already has the stored palette.
        try
            match JS.Window.LocalStorage.GetItem(themeKey) with
            | "light" | "dark" as stored -> theme.Value <- stored
            | _ -> ()
        with _ ->
            ()
        applyTheme ()

        // `.Bind()` renders the whole document body; the cookie table is a
        // reactive `ws-hole` bound to `cookies`.
        IndexTemplate()
            .themeLabel(themeLabel.View)
            .cookieName(cookieName)
            .cookieValue(cookieValue)
            .attrPath(attrPath)
            .attrDomain(attrDomain)
            .attrExpires(attrExpires)
            .attrSameSite(attrSameSite)
            .attrSecure(attrSecure)
            .readName(readName)
            .status(status.View)
            .lastResult(lastResult.View)
            .rawDocumentCookie(rawDocumentCookie.View)
            .attributesText(attributesText.View)
            .converterText(converterText.View)
            .jarCount(jarCount.View)
            .cookieRows(
                Doc.BindSeqCached
                    (fun (name, value) ->
                        tr [] [
                            td [ attr.``class`` "cookie-name" ] [ Doc.TextNode name ]
                            td [ attr.``class`` "cookie-value" ] [ Doc.TextNode value ]
                        ])
                    cookies.View)
            .toggleTheme(fun _ -> toggleTheme ())
            .save(fun _ -> save ())
            .read(fun _ -> read ())
            .readAll(fun _ -> readAll ())
            .remove(fun _ -> remove ())
            .scopedSet(fun _ -> scopedSet ())
            .converted(fun _ -> converted ())
            .noConflict(fun _ -> noConflict ())
            .refresh(fun _ -> refresh ())
            .Bind()

        refresh ()
