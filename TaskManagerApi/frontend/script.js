const apiUrl = "https://localhost:7133/api"; 

document.addEventListener("DOMContentLoaded", () => {
    if (window.location.pathname.includes("index.html") || window.location.pathname === "/") {
        const token = localStorage.getItem("jwtToken");

        if (!token) {
            window.location.href = "login.html";
        } else {
            const userDisplay = document.getElementById("userDisplay");
            const savedName = localStorage.getItem("username");
            if (userDisplay && savedName) {
                userDisplay.textContent = `👤 ${savedName}`;
            }
            getTasks();
        }
    }
});

function getHeaders() {
    const token = localStorage.getItem("jwtToken");
    return {
        "Content-Type": "application/json",
        "Authorization": `Bearer ${token}`
    };
}

function handleApiError(response) {
    if (response.status === 401) {
        alert("Oturum süreniz doldu, lütfen tekrar giriş yapın.");
        logout();
        return true; 
    }
    return false; 
}

async function register() {
    const usernameInput = document.getElementById("reg-username");
    const passwordInput = document.getElementById("reg-password");

    if (!usernameInput.value || !passwordInput.value) {
        alert("Lütfen tüm alanları doldurun!");
        return;
    }

    try {
        const response = await fetch(`${apiUrl}/Auth/register`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                username: usernameInput.value,
                password: passwordInput.value
            })
        });

        if (response.ok) {
            alert("✅ Kayıt başarılı! Giriş yapabilirsiniz.");
            window.location.href = "login.html";
        } else {
            const msg = await response.text();
            alert("❌ Kayıt hatası: " + msg);
        }
    } catch (error) {
        console.error(error);
        alert("Sunucuya bağlanılamadı.");
    }
}

async function login() {
    const usernameInput = document.getElementById("login-username");
    const passwordInput = document.getElementById("login-password");

    try {
        const response = await fetch(`${apiUrl}/Auth/login`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                username: usernameInput.value,
                password: passwordInput.value
            })
        });

        if (response.ok) {
            const data = await response.json();
            // Token ve kullanıcı adını sakla
            localStorage.setItem("jwtToken", data.token);
            localStorage.setItem("username", usernameInput.value);
            window.location.href = "index.html";
        } else {
            alert("❌ Kullanıcı adı veya şifre hatalı!");
        }
    } catch (error) {
        console.error(error);
        alert("Sunucu hatası.");
    }
}

function logout() {
    if (confirm("Çıkış yapmak istediğinize emin misiniz?")) {
        localStorage.removeItem("jwtToken");
        localStorage.removeItem("username");
        window.location.href = "login.html";
    }
}

async function getTasks() {
    const searchVal = document.getElementById("searchInput").value;
    const statusVal = document.getElementById("statusFilter").value;

    try {
        const response = await fetch(`${apiUrl}/Tasks?search=${searchVal}&status=${statusVal}`, {
            method: "GET",
            headers: getHeaders()
        });

        if (handleApiError(response)) return;

        const tasks = await response.json();
        renderTasks(tasks);
    } catch (error) {
        console.error("Görevler yüklenirken hata:", error);
    }
}

function renderTasks(tasks) {
    const list = document.getElementById("taskList");
    list.innerHTML = ""; 

    if (tasks.length === 0) {
        list.innerHTML = `<li style="text-align:center; color:#777; padding:20px;">Henüz bir görev yok.</li>`;
        return;
    }

    tasks.forEach(task => {
        const li = document.createElement("li");
        li.className = "task-item";

        const safeTitle = task.title.replace(/'/g, "\\'");
        const safeDesc = (task.description || "").replace(/'/g, "\\'");

        // Tamamlandıysa üstünü çiz
        const textStyle = task.isCompleted
            ? 'text-decoration: line-through; color: #777;'
            : 'color: white; font-weight: bold;';

        const btnClass = task.isCompleted ? 'btn-secondary' : 'btn-success';
        const btnText = task.isCompleted ? 'Geri Al' : '✔';

        li.innerHTML = `
            <div class="task-info">
                <span class="task-title" style="${textStyle}">${task.title}</span>
                <span class="task-desc">${task.description || ""}</span>
            </div>
            <div class="task-actions">
                <button class="btn btn-warning btn-sm" onclick="startEditMode(${task.id}, '${safeTitle}', '${safeDesc}', ${task.isCompleted})">✏️</button>
                <button class="btn btn-danger btn-sm" onclick="deleteTask(${task.id})">🗑️</button>
                <button class="btn ${btnClass} btn-sm" onclick="toggleTaskStatus(${task.id}, '${safeTitle}', '${safeDesc}', ${!task.isCompleted})">
                    ${btnText}
                </button>
            </div>
        `;
        list.appendChild(li);
    });
}

let isEditing = false;
let editingId = null;
let editingStatus = false; 
async function saveTask() {
    const titleInput = document.getElementById("taskTitle");
    const descInput = document.getElementById("taskDesc");

    if (!titleInput.value.trim()) {
        alert("Lütfen bir başlık girin!");
        return;
    }

    const taskData = {
        title: titleInput.value,
        description: descInput.value,
        isCompleted: isEditing ? editingStatus : false 
    };

    let url = `${apiUrl}/Tasks`;
    let method = "POST";

    if (isEditing) {
        url = `${apiUrl}/Tasks/${editingId}`;
        method = "PUT";
        taskData.id = editingId;
    }

    try {
        const response = await fetch(url, {
            method: method,
            headers: getHeaders(),
            body: JSON.stringify(taskData)
        });

        if (handleApiError(response)) return;

        if (response.ok) {
            resetForm(); 
            getTasks();  
        } else {
            alert("İşlem başarısız oldu.");
        }
    } catch (error) {
        console.error("Hata:", error);
    }
}

async function deleteTask(id) {
    if (!confirm("Bu görevi silmek istediğinize emin misiniz?")) return;

    try {
        const response = await fetch(`${apiUrl}/Tasks/${id}`, {
            method: "DELETE",
            headers: getHeaders()
        });

        if (handleApiError(response)) return;
        getTasks();
    } catch (error) {
        console.error("Silme hatası:", error);
    }
}

async function toggleTaskStatus(id, currentTitle, currentDesc, newStatus) {
    const taskData = {
        id: id,
        title: currentTitle,       
        description: currentDesc,  
        isCompleted: newStatus     
    };

    try {
        const response = await fetch(`${apiUrl}/Tasks/${id}`, {
            method: "PUT",
            headers: getHeaders(),
            body: JSON.stringify(taskData)
        });

        if (handleApiError(response)) return;
        getTasks();
    } catch (error) {
        console.error("Durum güncelleme hatası:", error);
    }
}

function startEditMode(id, title, desc, status) {
    isEditing = true;
    editingId = id;
    editingStatus = status; 

    document.getElementById("taskTitle").value = title;
    document.getElementById("taskDesc").value = desc;

    const saveBtn = document.getElementById("saveBtn");
    const cancelBtn = document.getElementById("cancelBtn");

    saveBtn.textContent = "GÜNCELLE";
    saveBtn.classList.remove("btn-primary");
    saveBtn.classList.add("btn-warning");

    cancelBtn.style.display = "inline-block"; 
}

function resetForm() {
    isEditing = false;
    editingId = null;
    editingStatus = false;

    document.getElementById("taskTitle").value = "";
    document.getElementById("taskDesc").value = "";

    const saveBtn = document.getElementById("saveBtn");
    const cancelBtn = document.getElementById("cancelBtn");

    saveBtn.textContent = "EKLE";
    saveBtn.classList.remove("btn-warning");
    saveBtn.classList.add("btn-primary");

    cancelBtn.style.display = "none";
}