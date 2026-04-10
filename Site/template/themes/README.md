# Website Themes

Denna mapp innehåller 10 färgteman för genererade hemsidor. Alla teman har samma struktur och layout, men olika färgscheman.

## Tillgängliga Teman

1. **Modern Purple** (standard) - Lila & Magenta
   - Fil: `styles-purple.css`
   - Färger: #6a1b9a (navy), #ab47bc (accent)
   - Användning: Modernt, kreativt, lyxigt

2. **Forest** (original) - Navy & Forest Green
   - Fil: `styles-forest.css`
   - Färger: #243A4A (navy), #1F3A2E (forest)
   - Användning: Professionellt, naturligt, pålitligt

3. **Dark Mode** - Mörkt tema
   - Fil: `styles-dark.css`
   - Färger: #1a1a2e (navy), #e94560 (accent)
   - Användning: Modern, tech-fokuserad, nattläge

4. **Ocean** - Blå & Turkos
   - Fil: `styles-ocean.css`
   - Färger: #0b3d61 (navy), #0fa3b1 (accent)
   - Användning: Fräscht, rent, maritim

5. **Nordic** - Ljus & Minimalistisk
   - Fil: `styles-nordic.css`
   - Färger: Ljusa, avskalade toner
   - Användning: Skandinavisk design, minimalism

6. **Warm** - Varm & Jordnära
   - Fil: `styles-warm.css`
   - Färger: Varma, jordnära toner
   - Användning: Välkomnande, hemtrevlig

7. **Sunset** - Orange & Coral
   - Fil: `styles-sunset.css`
   - Färger: #d84315 (navy), #ff6f00 (accent)
   - Användning: Energisk, varm, kreativ

8. **Mint** - Mintgrön & Jade
   - Fil: `styles-mint.css`
   - Färger: #00695c (navy), #26a69a (accent)
   - Användning: Fräsch, lugnande, naturlig

9. **Rose** - Rosa & Burgundy
   - Fil: `styles-rose.css`
   - Färger: #880e4f (navy), #c2185b (accent)
   - Användning: Elegant, feminin, lyxig

10. **Slate** - Grå & Blågrå
    - Fil: `styles-slate.css`
    - Färger: #455a64 (navy), #607d8b (accent)
    - Användning: Professionell, neutral, företag

## Hur man byter tema

### Metod 1: Ändra i index.html (manuellt)

Öppna `index.html` och ändra CSS-länken:

```html
<!-- Från: -->
<link rel="stylesheet" href="styles.css">

<!-- Till: -->
<link rel="stylesheet" href="themes/styles-ocean.css">
```

### Metod 2: Via API (framtida feature)

I framtiden kan du skicka med tema-val i API-requesten:

```json
{
  "orgNumber": "556XXX-XXXX",
  "customization": {
    "theme": "ocean"
  }
}
```

## Tema-struktur

Alla teman följer samma struktur med CSS-variabler:

```css
:root {
    --navy: #huvudfärg;
    --navy-dark: #mörkare-variant;
    --forest: #accentfärg;
    --forest-light: #ljusare-accent;
    --white: #ffffff;
    --light-grey: #bakgrund;
    --text-main: #huvudtext;
    --text-muted: #sekundärtext;
    --border-radius: 8px;
    --shadow: 0 4px 6px rgba(...);
    --transition: 0.3s ease;
}
```

## Skapa eget tema

1. Kopiera en befintlig tema-fil (t.ex. `styles-purple.css`)
2. Byt namn till `styles-[ditt-tema].css`
3. Ändra färgvariablerna i `:root`
4. Testa genom att länka till den i `index.html`

## Automatisk kopiering

När en hemsida genereras kopieras automatiskt:
- `styles.css` (standardtema)
- `app.js` (JavaScript)
- `themes/` (alla teman)

Detta gör att användare kan byta tema efter generering genom att ändra CSS-länken.

## Färgval-tips

- **Navy** (--navy): Huvudfärg för header, rubriker, footer
- **Forest** (--forest): Accentfärg för knappar, ikoner, highlights
- **Light Grey** (--light-grey): Bakgrundsfärg för sektioner
- **Text Main** (--text-main): Huvudtextfärg
- **Text Muted** (--text-muted): Sekundär text, beskrivningar

## Tillgänglighet

Alla teman är designade med tillgänglighet i åtanke:
- Kontrast mellan text och bakgrund följer WCAG AA-standard
- Färgblinda-vänliga färgkombinationer
- Tydlig visuell hierarki

## Support

För frågor eller problem med teman, kontakta utvecklingsteamet.
