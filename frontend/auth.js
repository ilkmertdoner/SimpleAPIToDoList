const apiUrl = "https://localhost:7133/api";

document.addEventListener("DOMContentLoaded", () => {
    const savedTheme = localStorage.getItem('theme');
    const themeIcon = document.getElementById('themeIcon');
    const html = document.documentElement;

    if (savedTheme === 'light') {
        html.classList.remove('dark');
        if (themeIcon) {
            themeIcon.textContent = '☀️';
            themeIcon.style.color = '#f59e0b';
        }
    } else {
        html.classList.add('dark');
        if (themeIcon) {
            themeIcon.textContent = '🌙';
            themeIcon.style.color = '#fbbf24';
        }
    }
});

function toggleTheme() {
    const html = document.documentElement;
    const themeIcon = document.getElementById('themeIcon');

    if (html.classList.contains('dark')) {
        html.classList.remove('dark');
        if (themeIcon) {
            themeIcon.textContent = '☀️';
            themeIcon.style.color = '#f59e0b';
        }
        localStorage.setItem('theme', 'light');
    } else {
        html.classList.add('dark');
        if (themeIcon) {
            themeIcon.textContent = '🌙';
            themeIcon.style.color = '#fbbf24';
        }
        localStorage.setItem('theme', 'dark');
    }
}

async function registerUser() {
    const usernameInput = document.getElementById("reg-username");
    const emailInput = document.getElementById("reg-email");
    const passwordInput = document.getElementById("reg-password");

    if (!usernameInput.value.trim() || !emailInput.value.trim() || !passwordInput.value.trim()) {
        alert("Lütfen tüm alanları doldurunuz.");
        return;
    }

    const userData = {
        username: usernameInput.value,
        email: emailInput.value,
        password: passwordInput.value
    };

    try {
        const response = await fetch(`${apiUrl}/Auth/register`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(userData)
        });

        if (response.ok) {
            alert("✅ Kayıt Başarılı! Giriş ekranına yönlendiriliyorsunuz...");
            window.location.href = "login.html";
        } else {
            const errorText = await response.text();
            alert("❌ Hata: " + errorText);
        }
    } catch (error) {
        console.error("Bağlantı Hatası:", error);
        alert("Sunucuya bağlanılamadı!");
    }
}

async function loginUser() {
    const usernameInput = document.getElementById("login-username");
    const passwordInput = document.getElementById("login-password");

    if (!usernameInput.value.trim() || !passwordInput.value.trim()) {
        alert("Lütfen Alanları Doldurunuz.");
        return;
    }

    const loginData = {
        username: usernameInput.value,
        password: passwordInput.value
    };

    try {
        const response = await fetch(`${apiUrl}/Auth/login`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(loginData)
        });

        if (response.ok) {
            const data = await response.json();

            if (data.token) {
                localStorage.setItem("jwtToken", data.token);
                localStorage.setItem("username", data.username || usernameInput.value);

                alert("Giriş Başarılı! Yönlendiriliyorsunuz...");
                window.location.href = "index.html";
            } else {
                console.error("Token bulunamadı. Yanıtı kontrol et:", data);
                alert("Sistemsel bir hata oluştu: Token alınamadı.");
            }
        } else {
            const errorMsg = await response.text();
            alert("❌ Giriş Başarısız: " + errorMsg);
        }
    } catch (error) {
        console.error("Sunucu Hatası:", error);
        alert("Sunucuya bağlanılamadı!");
    }
}