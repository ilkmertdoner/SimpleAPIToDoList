const apiUrl = "https://localhost:7133/api";

async function registerUser() {
    const usernameInput = document.getElementById("reg-username");
    const passwordInput = document.getElementById("reg-password");

    const userData = {
        username: usernameInput.value,
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
        console.error(error);
        alert("Sunucuya bağlanılamadı!");
    }
}

async function loginUser() {
    const usernameInput = document.getElementById("login-username");
    const passwordInput = document.getElementById("login-password");

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

            localStorage.setItem("jwtToken", data.token);

            alert("Giriş Başarılı! Yönlendiriliyorsunuz...");
            window.location.href = "index.html";
        } else {
            alert("❌ Kullanıcı adı veya şifre hatalı!");
        }
    } catch (error) {
        console.error(error);
        alert("Sunucu hatası!");
    }
}