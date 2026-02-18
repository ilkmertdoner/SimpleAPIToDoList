const apiUrl = "https://localhost:7133/api";

document.addEventListener("DOMContentLoaded", () => {
    const savedTheme = localStorage.getItem('theme');
    const themeIcon = document.getElementById('themeIcon');
    const html = document.documentElement;

    if (savedTheme === 'light') {
        html.classList.remove('dark');
        if (themeIcon) { themeIcon.textContent = '☀️'; themeIcon.style.color = '#f59e0b'; }
    } else {
        html.classList.add('dark');
        if (themeIcon) { themeIcon.textContent = '🌙'; themeIcon.style.color = '#fbbf24'; }
    }

    if (window.location.pathname.includes("index.html") || window.location.pathname === "/") {
        const token = localStorage.getItem("jwtToken");
        if (!token) {
            window.location.href = "login.html";
        } else {
            const userDisplay = document.getElementById("userDisplay");
            const savedName = localStorage.getItem("username");
            if (userDisplay && savedName) userDisplay.textContent = `👤 ${savedName}`;
            getTasks();
        }
    }
});

function toggleTheme() {
    const html = document.documentElement;
    const themeIcon = document.getElementById('themeIcon');
    if (html.classList.contains('dark')) {
        html.classList.remove('dark');
        themeIcon.textContent = '☀️';
        themeIcon.style.color = '#f59e0b';
        localStorage.setItem('theme', 'light');
    } else {
        html.classList.add('dark');
        themeIcon.textContent = '🌙';
        themeIcon.style.color = '#fbbf24';
        localStorage.setItem('theme', 'dark');
    }
}

function toggleSidebar() {
    const sidebar = document.getElementById('sidebar');
    const overlay = document.getElementById('sidebarOverlay');
    if (window.innerWidth < 768) {
        if (sidebar.classList.contains('-translate-x-full')) {
            sidebar.classList.remove('-translate-x-full');
            overlay.classList.remove('hidden');
        } else {
            sidebar.classList.add('-translate-x-full');
            overlay.classList.add('hidden');
        }
    } else {
        if (sidebar.classList.contains('w-0')) {
            sidebar.classList.remove('w-0', 'p-0', 'border-none', 'overflow-hidden');
            sidebar.classList.add('w-64');
        } else {
            sidebar.classList.add('w-0', 'p-0', 'border-none', 'overflow-hidden');
            sidebar.classList.remove('w-64');
        }
    }
}

function getHeaders() {
    const token = localStorage.getItem("jwtToken");
    return { "Content-Type": "application/json", "Authorization": `Bearer ${token}` };
}

function handleApiError(response) {
    if (response.status === 401) {
        localStorage.removeItem("jwtToken");
        window.location.href = "login.html";
        return true;
    }
    return !response.ok;
}

function logout() {
    if (confirm("Çıkış yapmak istediğinize emin misiniz?")) {
        localStorage.removeItem("jwtToken");
        window.location.href = "login.html";
    }
}

async function filterTasks(category) {
    const title = document.getElementById('pageTitle');
    const sidebar = document.getElementById('sidebar');
    const overlay = document.getElementById('sidebarOverlay');
    document.querySelectorAll('#sidebar button').forEach(btn => {
        btn.className = "w-full flex items-center gap-3 px-4 py-3 text-left rounded-xl text-slate-600 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-[#2c2c2c] transition-colors";
    });
    const activeBtn = document.querySelector(`#sidebar button[onclick="filterTasks('${category}')"]`);
    if (activeBtn) activeBtn.className = "w-full flex items-center gap-3 px-4 py-3 text-left rounded-xl bg-blue-50 dark:bg-blue-900/20 text-blue-600 dark:text-blue-400 font-medium hover:bg-blue-100 dark:hover:bg-blue-900/30 transition-colors";
    if (window.innerWidth < 768) {
        if (sidebar) sidebar.classList.add('-translate-x-full');
        if (overlay) overlay.classList.add('hidden');
    }
    const filters = document.querySelectorAll('.filter-control');
    switch (category) {
        case 'all':
            title.textContent = "Tüm Görevler";
            filters.forEach(f => f.disabled = false);
            getTasks();
            break;
        case 'favorites':
            title.textContent = "⭐ Favoriler";
            filters.forEach(f => f.disabled = true);
            fetchAndRender(`${apiUrl}/Tasks/favorites`);
            break;
        case 'deleted':
            title.textContent = "🗑️ Çöp Kutusu";
            filters.forEach(f => f.disabled = true);
            fetchAndRender(`${apiUrl}/Tasks/bin`);
            break;
    }
}

async function fetchAndRender(url) {
    try {
        const response = await fetch(url, { method: "GET", headers: getHeaders() });
        if (handleApiError(response)) return;
        const tasks = await response.json();
        renderTasks(tasks);
    } catch (error) { console.error(error); }
}

async function getTasks() {
    const searchVal = document.getElementById("searchInput")?.value || "";
    const statusVal = document.getElementById("statusFilter")?.value || "all";
    const priorityVal = document.getElementById("priorityFilter")?.value || "";
    const sortOrder = document.getElementById("sortOrder")?.value || "default";
    const pageTitle = document.getElementById('pageTitle').textContent;
    if (pageTitle.includes("Favoriler")) { fetchAndRender(`${apiUrl}/Tasks/favorites`); return; }
    if (pageTitle.includes("Çöp Kutusu")) { fetchAndRender(`${apiUrl}/Tasks/bin`); return; }
    let queryUrl = `${apiUrl}/Tasks?search=${searchVal}&status=${statusVal}`;
    if (priorityVal) queryUrl += `&priority=${priorityVal}`;
    try {
        const response = await fetch(queryUrl, { method: "GET", headers: getHeaders() });
        if (handleApiError(response)) return;
        let tasks = await response.json();
        if (sortOrder === "dateAsc") {
            tasks.sort((a, b) => {
                const da = a.dueDate || a.DueDate; const db = b.dueDate || b.DueDate;
                if (!da) return 1; if (!db) return -1;
                return new Date(da) - new Date(db);
            });
        } else if (sortOrder === "dateDesc") {
            tasks.sort((a, b) => {
                const da = a.dueDate || a.DueDate; const db = b.dueDate || b.DueDate;
                if (!da) return 1; if (!db) return -1;
                return new Date(db) - new Date(da);
            });
        } else if (sortOrder === "priorityDesc") {
            tasks.sort((a, b) => (b.priority || b.Priority) - (a.priority || a.Priority));
        }
        renderTasks(tasks);
    } catch (error) { console.error(error); }
}

function renderTasks(tasks) {
    const list = document.getElementById("taskList");
    if (!list) return;
    list.innerHTML = "";

    const searchVal = document.getElementById("searchInput")?.value || "";
    const statusVal = document.getElementById("statusFilter")?.value || "all";
    const priorityVal = document.getElementById("priorityFilter")?.value || "";
    const pageTitle = document.getElementById('pageTitle').textContent;
    const canDrag = pageTitle === "Tüm Görevler" && searchVal === "" && statusVal === "all" && priorityVal === "";

    if (tasks.length === 0) {
        list.innerHTML = '<div class="text-center text-slate-500 py-10">Görev bulunamadı.</div>';
        return;
    }

    const now = new Date(); 

    tasks.forEach(task => {
        const tTitle = task.title || task.Title;
        const tDesc = task.description || task.Description;
        const tPriority = task.priority !== undefined ? task.priority : task.Priority;
        const tDate = task.dueDate || task.DueDate;
        const tIsCompleted = task.isCompleted !== undefined ? task.isCompleted : task.IsCompleted;
        const tIsFavorite = task.isFavorite;
        const tIsDeleted = task.isDeleted;

        let dateBadge = "";
        let borderClass = "border-slate-200 dark:border-slate-700"; 

        if (tDate) {
            const d = new Date(tDate);
            const isMidnight = d.getHours() === 0 && d.getMinutes() === 0;
            const options = isMidnight ? { day: 'numeric', month: 'short' } : { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' };
            const dStr = d.toLocaleDateString('tr-TR', options);

            const diffMs = d - now;
            const isOverdue = diffMs < 0; 
            const isUpcoming = diffMs > 0 && diffMs < (24 * 60 * 60 * 1000); 

            let dateColorClass = "text-slate-400";
            let dateIcon = "📅";

            if (!tIsCompleted) {
                if (isOverdue) {
                    dateColorClass = "text-red-600 dark:text-red-400 font-bold";
                    borderClass = "border-red-300 dark:border-red-900 border-2";
                    dateIcon = "⚠️ Gecikti:";
                } else if (isUpcoming) {
                    dateColorClass = "text-amber-600 dark:text-amber-400 font-bold";
                    dateIcon = "⏳ Yaklaşıyor:";
                }
            } else {
                dateColorClass = "text-emerald-600/70 dark:text-emerald-400/70";
            }

            dateBadge = `<span class="text-xs ${dateColorClass} flex items-center gap-1">${dateIcon} ${dStr}</span>`;
        }

        const li = document.createElement("li");

        li.className = `task-item group flex flex-col p-4 mb-3 border rounded-xl shadow-sm transition-all ${canDrag ? 'cursor-move hover:scale-[1.005]' : 'cursor-default'} ${tIsCompleted ? 'bg-emerald-50/90 dark:bg-emerald-900/20 border-emerald-200' : `bg-white dark:bg-[#252525] ${borderClass}`}`;

        li.draggable = canDrag;
        li.dataset.id = task.id || task.Id;

        if (canDrag) {
            li.addEventListener('dragstart', () => li.classList.add('dragging', 'opacity-50'));
            li.addEventListener('dragend', () => li.classList.remove('dragging', 'opacity-50'));
        }

        let priorityBadge = "";
        if (tPriority === 1) priorityBadge = `<span class="px-2 py-0.5 rounded text-xs font-bold bg-blue-100 text-blue-600">Düşük</span>`;
        else if (tPriority === 2) priorityBadge = `<span class="px-2 py-0.5 rounded text-xs font-bold bg-yellow-100 text-yellow-600">Orta</span>`;
        else if (tPriority === 3) priorityBadge = `<span class="px-2 py-0.5 rounded text-xs font-bold bg-red-100 text-red-600">Yüksek</span>`;

        const safeTitle = tTitle ? tTitle.replace(/'/g, "\\'") : "";
        const safeDesc = tDesc ? tDesc.replace(/'/g, "\\'") : "";
        const safeDate = tDate || "";
        const checkClass = tIsCompleted ? "bg-emerald-500 border-emerald-500 text-white" : "border-slate-300 text-transparent hover:border-emerald-500";
        const titleStyle = tIsCompleted ? "line-through text-slate-400" : "text-slate-900 dark:text-white";
        const starIcon = tIsFavorite ?
            `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" class="w-6 h-6 text-yellow-400"><path fill-rule="evenodd" d="M10.788 3.21c.448-1.077 1.976-1.077 2.424 0l2.082 5.007 5.404.433c1.164.093 1.636 1.545.749 2.305l-4.117 3.527 1.257 5.273c.271 1.136-.964 2.033-1.96 1.425L12 18.354 7.373 21.18c-.996.608-2.231-.29-1.96-1.425l1.257-5.273-4.117-3.527c-.887-.76-.415-2.212.749-2.305l5.404-.433 2.082-5.006z" clip-rule="evenodd" /></svg>` :
            `<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-6 h-6 text-slate-300 hover:text-yellow-400"><path stroke-linecap="round" stroke-linejoin="round" d="M11.48 3.499a.562.562 0 011.04 0l2.125 5.111a.563.563 0 00.475.345l5.518.442c.563.044.889.77.448 1.152l-4.204 3.614a.562.562 0 00-.182.557l1.285 5.385a.562.562 0 01-.84.61l-4.725-2.885a.563.563 0 00-.586 0L6.982 20.54a.562.562 0 01-.84-.61l1.285-5.386a.562.562 0 00-.182-.557l-4.204-3.614a.562.562 0 01.448-1.152l5.518-.442a.563.563 0 00.475-.345L11.48 3.5z" /></svg>`;

        li.innerHTML = `
            <div class="flex items-center w-full gap-4">
                <button class="w-6 h-6 rounded-full border-2 flex items-center justify-center text-xs font-bold transition-all flex-shrink-0 ${checkClass}" 
                        onclick="event.stopPropagation(); toggleStatus(${task.id || task.Id}, '${safeTitle}', '${safeDesc}', ${!tIsCompleted}, ${tIsFavorite}, ${tPriority}, '${safeDate}')">
                    ${tIsCompleted ? '✔' : ''}
                </button>
                <div class="flex-grow min-w-0">
                    <div class="flex items-center gap-2 mb-1">
                        ${priorityBadge}
                        ${dateBadge} </div>
                    <span class="block font-bold truncate text-lg ${titleStyle}">${tTitle}</span>
                    <span class="block text-sm text-slate-500 dark:text-slate-400 truncate mt-0.5">${tDesc}</span>
                </div>
                <div class="flex items-center gap-2">
                    ${!tIsDeleted ? `
                        <button class="p-1" onclick="event.stopPropagation(); toggleFavorite(${task.id || task.Id})">${starIcon}</button>
                        <div class="flex gap-1 ml-2 border-l pl-3 border-slate-200 dark:border-slate-700">
                            <button class="p-2 text-amber-500 hover:bg-amber-100 rounded-lg" onclick="event.stopPropagation(); startEditMode('${task.id || task.Id}', '${safeTitle}', '${safeDesc}', ${tIsCompleted}, ${tIsFavorite}, ${tPriority}, '${safeDate}')">✏️</button>
                            <button class="p-2 text-red-500 hover:bg-red-100 rounded-lg" onclick="event.stopPropagation(); toggleBin(${task.id || task.Id})">🗑️</button>
                        </div>
                    ` : `
                        <button class="px-3 py-1 text-blue-500 border border-blue-500 rounded-lg text-xs font-bold hover:bg-blue-500 hover:text-white transition-all" onclick="event.stopPropagation(); toggleBin(${task.id || task.Id})">GERİ YÜKLE</button>
                        <button class="px-3 py-1 text-red-500 border border-red-500 rounded-lg text-xs font-bold hover:bg-red-500 hover:text-white transition-all" onclick="event.stopPropagation(); deleteTask(${task.id || task.Id})">SİL</button>
                    `}
                </div>
            </div>
        `;
        list.appendChild(li);
    });
    if (canDrag) initDragAndDrop();
}

async function toggleFavorite(id) { await fetch(`${apiUrl}/Tasks/favorites/${id}`, { method: "PUT", headers: getHeaders() }); getTasks(); }
async function toggleBin(id) { await fetch(`${apiUrl}/Tasks/bin/${id}`, { method: "PUT", headers: getHeaders() }); getTasks(); }
async function deleteTask(id) { if (confirm("Kalıcı silinsin mi?")) { await fetch(`${apiUrl}/Tasks/${id}`, { method: "DELETE", headers: getHeaders() }); getTasks(); } }

let isEditing = false;
let editingId = null;
let editingStatus = false;
let editingFavorite = false;

async function saveTask() {
    const titleInput = document.getElementById("taskTitle");
    const descInput = document.getElementById("taskDesc");
    const priorityInput = document.getElementById("taskPriority");
    const dateOnlyInput = document.getElementById("taskDateInput");
    const timeOnlyInput = document.getElementById("taskTimeInput");
    if (!titleInput.value.trim()) { alert("Başlık girin!"); return; }
    let finalDueDate = null;
    if (dateOnlyInput.value) {
        const timePart = timeOnlyInput.value ? timeOnlyInput.value : "00:00";
        finalDueDate = `${dateOnlyInput.value}T${timePart}`;
    }
    const taskData = {
        Title: titleInput.value,
        Description: descInput.value,
        Priority: parseInt(priorityInput.value) || 0,
        DueDate: finalDueDate,
        IsCompleted: isEditing ? editingStatus : false,
        isFavorite: isEditing ? editingFavorite : false
    };
    let url = `${apiUrl}/Tasks`;
    let method = "POST";
    if (isEditing) {
        url = `${apiUrl}/Tasks/${editingId}`;
        method = "PUT";
        taskData.Id = editingId;
        delete taskData.isFavorite;
    }
    try {
        const response = await fetch(url, { method: method, headers: getHeaders(), body: JSON.stringify(taskData) });
        if (response.ok) { resetForm(); getTasks(); }
    } catch (error) { console.error(error); }
}

async function toggleStatus(id, title, desc, status, isFav, priority, dueDate) {
    await fetch(`${apiUrl}/Tasks/${id}`, {
        method: "PUT",
        headers: getHeaders(),
        body: JSON.stringify({
            Id: id,
            Title: title,
            Description: desc,
            IsCompleted: status,
            isFavorite: isFav,
            Priority: priority,
            DueDate: dueDate && dueDate !== "null" ? dueDate : null
        })
    });
    getTasks();
}

function startEditMode(id, title, desc, status, isFav, priority, dueDate) {
    isEditing = true;
    editingId = id;
    editingStatus = status;
    editingFavorite = isFav;
    document.getElementById("taskTitle").value = title;
    document.getElementById("taskDesc").value = desc;
    if (priority) document.getElementById("taskPriority").value = priority;
    if (dueDate && dueDate !== "null" && dueDate !== "undefined") {
        const parts = dueDate.split('T');
        document.getElementById("taskDateInput").value = parts[0];
        if (parts[1] && !parts[1].startsWith("00:00")) document.getElementById("taskTimeInput").value = parts[1].substring(0, 5);
        else document.getElementById("taskTimeInput").value = "";
    } else {
        document.getElementById("taskDateInput").value = "";
        document.getElementById("taskTimeInput").value = "";
    }
    const saveBtn = document.getElementById("saveBtn");
    const cancelBtn = document.getElementById("cancelBtn");
    const formTitle = document.getElementById("formTitle");
    if (formTitle) formTitle.textContent = "Görevi Düzenle";
    if (saveBtn) {
        saveBtn.textContent = "GÜNCELLE";
        saveBtn.classList.replace("bg-blue-600", "bg-amber-500");
    }
    if (cancelBtn) cancelBtn.classList.remove("hidden");
    document.getElementById("taskTitle").focus();
}

function resetForm() {
    isEditing = false;
    editingId = null;
    editingStatus = false;
    editingFavorite = false;
    document.getElementById("taskTitle").value = "";
    document.getElementById("taskDesc").value = "";
    document.getElementById("taskPriority").value = "2";
    document.getElementById("taskDateInput").value = "";
    document.getElementById("taskTimeInput").value = "";
    const saveBtn = document.getElementById("saveBtn");
    const cancelBtn = document.getElementById("cancelBtn");
    const formTitle = document.getElementById("formTitle");
    if (formTitle) formTitle.textContent = "Yeni Görev";
    if (saveBtn) {
        saveBtn.textContent = "EKLE";
        saveBtn.classList.replace("bg-amber-500", "bg-blue-600");
    }
    if (cancelBtn) cancelBtn.classList.add("hidden");
}

function initDragAndDrop() {
    const list = document.getElementById("taskList");
    if (!list) return;
    list.addEventListener('dragover', e => {
        e.preventDefault();
        const draggingItem = document.querySelector('.dragging');
        const afterElement = getDragAfterElement(list, e.clientY);
        if (afterElement == null) list.appendChild(draggingItem);
        else list.insertBefore(draggingItem, afterElement);
    });
}

function getDragAfterElement(container, y) {
    const draggableElements = [...container.querySelectorAll('.task-item:not(.dragging)')];
    return draggableElements.reduce((closest, child) => {
        const box = child.getBoundingClientRect();
        const offset = y - box.top - box.height / 2;
        if (offset < 0 && offset > closest.offset) return { offset: offset, element: child };
        else return closest;
    }, { offset: Number.NEGATIVE_INFINITY }).element;
}