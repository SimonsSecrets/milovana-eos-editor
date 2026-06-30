using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MilovanaEosEditor.Dsl;

/// <summary>
/// A tiny ordered JSON DOM whose writer reproduces, byte-for-byte, the output of the
/// <c>System.Web.Script.Serialization.JavaScriptSerializer</c> used by <c>Build-Tease.ps1</c>:
/// compact (no whitespace), object keys in insertion order, and the same character escaping —
/// <c>&lt; &gt; &amp; '</c> escaped to lowercase <c>< > & '</c>, <c>"</c> as
/// <c>\"</c>, <c>/</c> left literal, other non-ASCII (em dashes, accents) left literal. This keeps a
/// C#-exported <c>tease.json</c> identical to the PowerShell-built one (so the port is verifiable by a
/// plain diff), and lets the reused <c>galleries/files/editor</c> blocks round-trip unchanged.
/// </summary>
public abstract class EosNode
{
    public abstract void Write(StringBuilder sb);

    public string ToJson()
    {
        var sb = new StringBuilder();
        Write(sb);
        return sb.ToString();
    }

    /// <summary>Convert a parsed <see cref="JsonElement"/> (e.g. the reused tease.json blocks) into this
    /// DOM, preserving number formatting verbatim via <see cref="JsonElement.GetRawText"/>.</summary>
    public static EosNode From(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.Object => FromObject(el),
        JsonValueKind.Array => FromArray(el),
        JsonValueKind.String => new EosString(el.GetString() ?? ""),
        JsonValueKind.Number => new EosRaw(el.GetRawText()),
        JsonValueKind.True => new EosRaw("true"),
        JsonValueKind.False => new EosRaw("false"),
        _ => new EosRaw("null"),
    };

    private static EosObject FromObject(JsonElement el)
    {
        var o = new EosObject();
        foreach (JsonProperty p in el.EnumerateObject()) o.Add(p.Name, From(p.Value));
        return o;
    }

    private static EosArray FromArray(JsonElement el)
    {
        var a = new EosArray();
        foreach (JsonElement item in el.EnumerateArray()) a.Add(From(item));
        return a;
    }

    internal static void WriteString(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                // JavaScriptSerializer HTML-escapes these to lowercase \u00xx.
                case '<': sb.Append("\\u003c"); break;
                case '>': sb.Append("\\u003e"); break;
                case '&': sb.Append("\\u0026"); break;
                case '\'': sb.Append("\\u0027"); break;
                default:
                    if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c); // >= 0x20 incl. non-ASCII: literal UTF-8
                    break;
            }
        }
        sb.Append('"');
    }
}

/// <summary>JSON object with ordered keys (last write wins on a duplicate key, like the PS [ordered]).</summary>
public sealed class EosObject : EosNode
{
    private readonly List<string> _keys = new();
    private readonly Dictionary<string, EosNode> _map = new();

    public EosObject Add(string key, EosNode value)
    {
        if (!_map.ContainsKey(key)) _keys.Add(key);
        _map[key] = value;
        return this;
    }

    /// <summary>Adds only when <paramref name="value"/> is non-null (for optional say params).</summary>
    public EosObject AddIf(string key, EosNode? value)
    {
        if (value is not null) Add(key, value);
        return this;
    }

    public EosObject this[string key]
    {
        set => Add(key, value);
    }

    public IReadOnlyList<string> Keys => _keys;
    public bool ContainsKey(string key) => _map.ContainsKey(key);
    public EosNode? Get(string key) => _map.GetValueOrDefault(key);

    public override void Write(StringBuilder sb)
    {
        sb.Append('{');
        for (int i = 0; i < _keys.Count; i++)
        {
            if (i > 0) sb.Append(',');
            WriteString(sb, _keys[i]);
            sb.Append(':');
            _map[_keys[i]].Write(sb);
        }
        sb.Append('}');
    }
}

public sealed class EosArray : EosNode
{
    private readonly List<EosNode> _items = new();
    public EosArray Add(EosNode node) { _items.Add(node); return this; }
    public EosArray AddRange(IEnumerable<EosNode> nodes) { _items.AddRange(nodes); return this; }
    public int Count => _items.Count;
    public IReadOnlyList<EosNode> Items => _items;

    public override void Write(StringBuilder sb)
    {
        sb.Append('[');
        for (int i = 0; i < _items.Count; i++)
        {
            if (i > 0) sb.Append(',');
            _items[i].Write(sb);
        }
        sb.Append(']');
    }
}

public sealed class EosString : EosNode
{
    private readonly string _value;
    public EosString(string value) => _value = value;
    public string Value => _value;
    public override void Write(StringBuilder sb) => WriteString(sb, _value);
}

public sealed class EosBool : EosNode
{
    private readonly bool _value;
    public EosBool(bool value) => _value = value;
    public override void Write(StringBuilder sb) => sb.Append(_value ? "true" : "false");
}

/// <summary>A pre-rendered token (number literal, <c>true</c>/<c>false</c>/<c>null</c>) written verbatim.</summary>
public sealed class EosRaw : EosNode
{
    private readonly string _raw;
    public EosRaw(string raw) => _raw = raw;
    public override void Write(StringBuilder sb) => sb.Append(_raw);
}

/// <summary>Number helpers that render like JavaScriptSerializer (1.0 → "1", 12.5 → "12.5").</summary>
public static class EosNumber
{
    public static EosNode Int(long v) => new EosRaw(v.ToString(CultureInfo.InvariantCulture));

    public static EosNode Double(double v) => new EosRaw(v.ToString("R", CultureInfo.InvariantCulture));
}
