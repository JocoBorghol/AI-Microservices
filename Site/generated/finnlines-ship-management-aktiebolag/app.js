document.addEventListener('DOMContentLoaded', () => {

    // ── LOGO PREVIEW via localStorage ──────────────────────────────────────────
    (function applyLogoPreview() {
        const storedLogo = localStorage.getItem('custom_logo');
        if (storedLogo) {
            const logoImgs = document.querySelectorAll('#custom-logo, .custom-logo-footer');
            logoImgs.forEach(img => {
                img.src = storedLogo;
                img.style.display = 'block';
            });
            const companyTexts = document.querySelectorAll('#company-text, #footer-company-text');
            companyTexts.forEach(txt => {
                txt.style.display = 'none';
            });
        }
    })();

    // ── TEMA-PREVIEW via URL-param (?theme=ocean) ──────────────────────────────
    // Gör det möjligt för dashboarden att öppna en live-preview av ett tema
    // utan att spara något. Temat läses från URL-parametern och byter stylesheet.
    (function applyThemePreview() {
        const params = new URLSearchParams(window.location.search);
        const requestedTheme = params.get('theme');
        if (!requestedTheme) return;

        const allowed = [
            'original', 'dark', 'forest', 'ocean', 'nordic', 'warm',
            'sunset', 'mint', 'rose', 'slate', 'purple', 'terracotta'
        ];
        if (!allowed.includes(requestedTheme)) return;

        // Hitta befintlig stylesheet-länk och byt ut den
        const existing = document.querySelector('link[rel="stylesheet"][href$=".css"]:not([href*="font-awesome"]):not([href*="fonts.googleapis"])');
        if (existing) {
            const newHref = requestedTheme === 'original'
                ? 'styles.css'
                : `themes/styles-${requestedTheme}.css`;
            existing.setAttribute('href', newHref);
        }

        // Visa en liten preview-banner längst upp
        const banner = document.createElement('div');
        banner.id = 'theme-preview-banner';
        banner.innerHTML = `
            <span>🎨 Förhandsvisning: <strong>${requestedTheme}</strong></span>
            <span style="font-size:0.8rem; opacity:0.8">Detta är en förhandsvisning - stäng fliken för att återgå till dashboarden</span>
        `;
        Object.assign(banner.style, {
            position: 'fixed', top: '0', left: '0', right: '0', zIndex: '99999',
            background: 'rgba(0,0,0,0.85)', color: '#fff', padding: '0.6rem 1.5rem',
            display: 'flex', justifyContent: 'space-between', alignItems: 'center',
            fontSize: '0.9rem', fontFamily: 'sans-serif', backdropFilter: 'blur(8px)'
        });
        document.body.prepend(banner);
        document.body.style.paddingTop = '44px';
    })();
    // ──────────────────────────────────────────────────────────────────────────

    // Mobilmeny-logik
    const mobileMenuBtn = document.getElementById('mobile-menu');
    const navMenu = document.getElementById('nav-menu');

    if (mobileMenuBtn) {
        mobileMenuBtn.addEventListener('click', () => {
            navMenu.classList.toggle('active');
            const icon = mobileMenuBtn.querySelector('i');
            icon.classList.toggle('fa-bars');
            icon.classList.toggle('fa-times');
        });
    }

    // Stäng meny vid klick på länk (för mobil)
    document.querySelectorAll('.nav-links a').forEach(link => {
        link.addEventListener('click', () => {
            navMenu.classList.remove('active');
            const icon = mobileMenuBtn.querySelector('i');
            if(icon) {
                icon.classList.add('fa-bars');
                icon.classList.remove('fa-times');
            }
        });
    });

    // Scroll Animationer
    const observerOptions = { threshold: 0.1 };
    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('visible');
            }
        });
    }, observerOptions);

    document.querySelectorAll('.fade-in').forEach(el => observer.observe(el));

    // Kontaktformulär
    const form = document.getElementById('contact-form');
    const status = document.getElementById('form-status');

    if (form) {
        form.addEventListener('submit', (e) => {
            e.preventDefault();
            const btn = form.querySelector('button');
            const originalText = btn.innerText;
            
            btn.innerText = "Skickar...";
            btn.disabled = true;

            setTimeout(() => {
                status.innerHTML = `<i class="fas fa-check-circle"></i> Tack! Vi återkommer inom kort.`;
                Object.assign(status.style, {
                    color: "#1F3A2E", fontWeight: "bold", marginTop: "1rem", 
                    padding: "1rem", backgroundColor: "#d5f5e3", borderRadius: "4px", display: "block"
                });
                form.reset();
                btn.innerText = originalText;
                btn.disabled = false;
            }, 1500);
        });
    }

    // Sociala Medier
    document.querySelectorAll('.social-link').forEach(link => {
        link.addEventListener('click', (e) => {
            e.preventDefault();
            const toast = document.createElement('div');
            toast.innerHTML = "<i class='fas fa-info-circle'></i> Sociala medier kommer snart!";
            Object.assign(toast.style, {
                position: 'fixed', bottom: '20px', right: '20px', backgroundColor: '#243A4A',
                color: '#fff', padding: '1rem', borderRadius: '8px', zIndex: '9999', opacity: '0', transition: '0.3s'
            });
            document.body.appendChild(toast);
            requestAnimationFrame(() => toast.style.opacity = '1');
            setTimeout(() => { toast.style.opacity = '0'; setTimeout(() => toast.remove(), 300); }, 3000);
        });
    });
});