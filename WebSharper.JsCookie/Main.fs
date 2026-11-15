// $begin{copyright}
//
// This file is part of WebSharper
//
// Copyright (c) 2008-2026 IntelliFactory
//
// Licensed under the Apache License, Version 2.0 (the "License"); you
// may not use this file except in compliance with the License.  You may
// obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
// implied.  See the License for the specific language governing
// permissions and limitations under the License.
//
// $end{copyright}
namespace WebSharper.JsCookie

open WebSharper
open WebSharper.JavaScript
open WebSharper.InterfaceGenerator

/// <summary>Typed bindings for <a href="https://github.com/js-cookie/js-cookie">js-cookie</a>
/// 3.0.8, the small, battle-tested JavaScript API for handling browser cookies.</summary>
/// <remarks>
/// The binding is npm based: the <c>Cookies</c> object is the default import from the
/// <c>js-cookie</c> package (<c>import Cookies from "js-cookie"</c>). There is no global
/// <c>Cookies</c> object and no CDN <c>&lt;script&gt;</c>; the sample bundles the npm
/// package with esbuild. <c>Cookies.WithAttributes</c> and <c>Cookies.WithConverter</c>
/// return further API objects (typed as <c>CookiesApi</c>) that share the same surface.
/// <c>noConflict</c> is deliberately not bound: it only exists in the UMD/global build
/// of the package, not in the ES module the bundler resolves.
/// </remarks>
module Definition =

    // ------------------------------------------------------------------
    // Forward declarations
    // ------------------------------------------------------------------

    /// The default export of the npm package: the cookie API object itself.
    let Cookies = Class "Cookies"

    /// The shape returned by <c>Cookies.WithAttributes</c>/<c>Cookies.WithConverter</c>:
    /// the same members, but as an instance so a derived API can be stored and
    /// called directly.
    let CookiesApi = Class "CookiesApi"

    /// The object returned by <c>Cookies.Get()</c> (the whole cookie jar),
    /// keyed by cookie name.
    let CookieMap = Class "CookieMap"

    // ------------------------------------------------------------------
    // Enumerations
    // ------------------------------------------------------------------

    /// The <c>SameSite</c> attribute of a cookie. The canonical lowercase
    /// values are what the cookie specification and js-cookie emit.
    let SameSite =
        Pattern.EnumStrings "SameSite" [
            "strict"
            "lax"
            "none"
        ]

    // ------------------------------------------------------------------
    // Plain object shapes
    // ------------------------------------------------------------------

    /// <summary>The per-call (or default) cookie attributes accepted by
    /// <c>Cookies.Set</c>/<c>Get</c>/<c>Remove</c> and <c>Cookies.WithAttributes</c>.</summary>
    /// <remarks>
    /// <c>Expires</c> is either a number of days from now or an absolute
    /// <c>Date</c>; upstream also allows arbitrary RFC 6265 attributes through
    /// the string indexer.
    /// </remarks>
    let CookieAttributes =
        Pattern.Config "CookieAttributes" {
            Required = []
            Optional = [
                "expires", T<float> + T<Date>
                "path", T<string>
                "domain", T<string>
                "secure", T<bool>
                "sameSite", SameSite.Type + T<string>
            ]
        }

    /// The lower-level decoders/encoders used by the package. <c>Write</c> runs
    /// before a value is stored, <c>Read</c> after it is read back; both receive
    /// the value and the cookie name.
    let Converter =
        Pattern.Config "Converter" {
            Required = []
            Optional = [
                "read", (T<string> * T<string>) ^-> T<string>
                "write", (T<string> * T<string>) ^-> T<string>
            ]
        }

    // ------------------------------------------------------------------
    // Members
    // ------------------------------------------------------------------

    // Upstream allows arbitrary RFC 6265 attributes through a string indexer,
    // alongside the named fields above.
    CookieAttributes
    |+> Instance [
        Property "" T<obj> |> Indexed T<string>
    ]
    |> ignore

    // The whole cookie jar is a plain object keyed by cookie name.
    CookieMap
    |+> Instance [
        Property "" T<string> |> Indexed T<string>
    ]
    |> ignore

    // The default export of the npm package is the `Cookies` object, so its
    // members are static. `WithAttributes`/`WithConverter` return `CookiesApi`,
    // the same shape as an instance that can be stored and called directly.
    Cookies
    |+> Static [
        Getter "attributes" CookieAttributes
        |> WithComment
            "The default attributes every call starts from (a frozen object, \
             `{ path: '/' }` unless changed with `WithAttributes`)."

        Getter "converter" Converter
        |> WithComment
            "The converters of this instance — by default `encodeURIComponent`/`decodeURIComponent`."

        "set" => T<string>?``name`` * T<string>?``value`` * !? CookieAttributes?``options`` ^-> T<string>
        |> WithComment
            "Creates/overwrites the cookie `name`. Returns the string that was \
             written, or `undefined` if the cookie was rejected by the browser."

        "get" => T<string>?``name`` ^-> T<string>
        |> WithComment
            "Reads the cookie `name`, or `undefined` when it is absent."

        "get" => T<unit> ^-> CookieMap
        |> WithComment "Reads the whole cookie jar as a name to value map."

        "remove" => T<string>?``name`` * !? CookieAttributes?``options`` ^-> T<unit>
        |> WithComment
            "Removes the cookie `name`. Pass the same `Path`/`Domain` that were \
             used to set it, or the removal may not match."

        "withAttributes" => CookieAttributes?``attributes`` ^-> CookiesApi
        |> WithComment
            "Returns a new API object whose default attributes are merged with \
             the given ones. Per-call attributes still override them."

        "withConverter" => Converter?``converter`` ^-> CookiesApi
        |> WithComment
            "Returns a new API object that uses the given read/write converters. \
             The default string-based surface is preserved."
    ]
    |> ignore

    // The object returned by `WithAttributes`/`WithConverter`: the same methods
    // as instance members, so `api.Get(...)` compiles.
    CookiesApi
    |+> Instance [
        Getter "attributes" CookieAttributes
        Getter "converter" Converter

        "set" => T<string>?``name`` * T<string>?``value`` * !? CookieAttributes?``options`` ^-> T<string>
        "get" => T<string>?``name`` ^-> T<string>
        "get" => T<unit> ^-> CookieMap
        "remove" => T<string>?``name`` * !? CookieAttributes?``options`` ^-> T<unit>
        "withAttributes" => CookieAttributes?``attributes`` ^-> TSelf
        "withConverter" => Converter?``converter`` ^-> TSelf
    ]
    |> ignore

    // The default export of the npm package is the `Cookies` object itself.
    Cookies |> ImportDefault "js-cookie" |> ignore

    let Assembly =
        Assembly [
            Namespace "WebSharper.JsCookie" [
                Cookies
                CookiesApi
                CookieMap
                CookieAttributes
                Converter
                SameSite
            ]
        ]

[<Sealed>]
type Extension() =
    interface IExtension with
        member x.Assembly = Definition.Assembly

[<assembly: Extension(typeof<Extension>)>]
do ()
