// Tiny helpers for the landing page. Everything else is Blazor and CSS.

// Syntax highlighting uses highlight.js, vendored in lib/highlight/ rather than loaded
// from a CDN, so the site keeps working whatever happens to a third-party host. Only the
// grammars the repository's code blocks use are included; to add one, drop its file in
// lib/highlight/languages/ and list it here.
const HIGHLIGHT_LANGUAGES = ['csharp', 'bash', 'javascript', 'sql', 'json', 'powershell'];

let highlighter;
function loadHighlighter() {
    // Loaded on first use, once. <base href> makes this resolve correctly under the
    // GitHub Pages sub-path as well as locally.
    highlighter ??= (async () => {
        const load = async (path) => (await import(new URL(`lib/highlight/${path}`, document.baseURI))).default;
        const [hljs, ...grammars] = await Promise.all([
            load('core.min.js'),
            ...HIGHLIGHT_LANGUAGES.map(language => load(`languages/${language}.min.js`)),
        ]);
        HIGHLIGHT_LANGUAGES.forEach((language, i) => hljs.registerLanguage(language, grammars[i]));
        return hljs;
    })();
    return highlighter;
}

window.santaWorkshop = {
    // Colours the code blocks in rendered markdown. Purely decoration: if anything here
    // fails, the code is still perfectly readable, so it fails quietly.
    async highlight() {
        const blocks = [...document.querySelectorAll('.prose pre code[class*="language-"]')]
            .filter(block => !block.dataset.highlighted);
        if (blocks.length === 0) return;

        try {
            const hljs = await loadHighlighter();
            for (const block of blocks) {
                const language = [...block.classList].find(c => c.startsWith('language-'))?.slice('language-'.length);
                if (language && hljs.getLanguage(language)) hljs.highlightElement(block);
            }
        } catch {
            // Intentionally ignored; see above.
        }
    },

    // Rendered markdown arrives after an async fetch, so the browser's own "jump to
    // #fragment on load" has nothing to jump to yet. Scroll once the headings exist.
    // (Blazor's own handling of same-page "#hash" links is separate and needs nothing here.)
    scrollToId(id) {
        document.getElementById(id)?.scrollIntoView({ block: 'start' });
    }
};

// The skip link ("Skip to content") has to move keyboard focus into <main>, not just scroll
// there — otherwise the next Tab starts again from the top of the page and the link has
// achieved nothing. Blazor scrolls to #main for us; focusing it is on us.
document.addEventListener('click', event => {
    if (event.target instanceof Element && event.target.closest('a.skip-link')) {
        document.getElementById('main')?.focus({ preventScroll: true });
    }
});
