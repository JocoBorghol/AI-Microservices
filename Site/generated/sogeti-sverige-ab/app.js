document.addEventListener('DOMContentLoaded', () => {
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