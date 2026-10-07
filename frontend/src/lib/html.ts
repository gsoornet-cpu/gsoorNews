// Pure helpers for reading article HTML (Slice 21, decision D2). No framework imports and no
// DOM access, so they run identically on the server (SEO metadata, JSON-LD) and in the browser
// (CMS counters), and can be exercised directly by a script.
//
// SECURITY NOTE: nothing here sanitizes. The API sanitizes with an allow-list before storing and
// the public page renders that stored value as markup (see article/[id]/page.tsx). These helpers
// only turn markup into TEXT — their output is always rendered by React (escaped) or put in JSON.

const NAMED_ENTITIES: Record<string, string> = {
  amp: "&",
  lt: "<",
  gt: ">",
  quot: '"',
  apos: "'",
  nbsp: " ",
};

// Tags that separate words even though no whitespace is written between them.
const WORD_BREAKING_TAGS = new Set([
  "p", "h1", "h2", "h3", "h4", "h5", "h6", "li", "ul", "ol", "blockquote", "div", "tr", "td", "br", "hr",
]);

function isAsciiLetter(ch: string): boolean {
  return (ch >= "a" && ch <= "z") || (ch >= "A" && ch <= "Z");
}

function isAsciiLetterOrDigit(ch: string): boolean {
  return isAsciiLetter(ch) || (ch >= "0" && ch <= "9");
}

function decodeEntities(text: string): string {
  return text.replace(/&(?:#(\d{1,7})|#[xX]([0-9a-fA-F]{1,6})|([a-zA-Z]+));/g, (whole, dec, hex, name) => {
    if (name) {
      return NAMED_ENTITIES[name.toLowerCase()] ?? whole;
    }

    const code = dec ? Number.parseInt(dec, 10) : Number.parseInt(hex, 16);
    // Reject anything that is not a valid scalar value (surrogates, > U+10FFFF, NUL).
    if (!Number.isFinite(code) || code === 0 || code > 0x10ffff || (code >= 0xd800 && code <= 0xdfff)) {
      return whole;
    }

    return code === 0xa0 ? " " : String.fromCodePoint(code);
  });
}

/**
 * Visible text of an HTML fragment: tags removed in ONE linear pass (a hand-written scanner, not
 * a regular expression, so no input can make it backtrack), block boundaries become spaces,
 * entities decoded, whitespace collapsed. Mirrors the API's ArticleBodyContent.ToPlainText so the
 * CMS counters, the SEO description and the server's excerpts agree.
 */
export function htmlToPlainText(html: string | null | undefined): string {
  if (!html) {
    return "";
  }

  let text = "";
  let i = 0;

  while (i < html.length) {
    const ch = html[i];
    const next = html[i + 1];

    // "<" starts a tag only before a letter, "/", "!" or "?" — otherwise ("a < b") it is text.
    if (ch !== "<" || next === undefined || !(isAsciiLetter(next) || next === "/" || next === "!" || next === "?")) {
      text += ch;
      i++;
      continue;
    }

    let j = i + 1;
    if (html[j] === "/") {
      j++;
    }

    const nameStart = j;
    while (j < html.length && isAsciiLetterOrDigit(html[j])) {
      j++;
    }

    const name = html.slice(nameStart, j).toLowerCase();

    let quote = "";
    for (; j < html.length; j++) {
      const c = html[j];
      if (quote) {
        if (c === quote) {
          quote = "";
        }
      } else if (c === '"' || c === "'") {
        quote = c;
      } else if (c === ">") {
        break;
      }
    }

    if (WORD_BREAKING_TAGS.has(name)) {
      text += " ";
    }

    i = j + 1; // an unterminated tag drops the rest of the input
  }

  return decodeEntities(text).replace(/\s+/g, " ").trim();
}

/** Whitespace-separated word count of already-extracted text (Arabic and Latin alike). */
export function countWords(text: string): number {
  const trimmed = text.trim();
  return trimmed ? trimmed.split(/\s+/).length : 0;
}
