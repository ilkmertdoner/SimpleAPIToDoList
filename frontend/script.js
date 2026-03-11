const apiUrl = "https://localhost:7133/api";
let globalTasks = [];
let myGroups = [];
let currentGroupId = null;
let currentGroupIsAdmin = false;

document.addEventListener("DOMContentLoaded", () => {
    const urlParams = new URLSearchParams(window.location.search);
    const urlToken = urlParams.get('token');
    const urlUser = urlParams.get('username');

    if (urlToken && urlUser) {
        localStorage.setItem('jwtToken', urlToken);
        localStorage.setItem('username', urlUser);
        window.history.replaceState({}, document.title, window.location.pathname);
    }

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

    const token = localStorage.getItem("jwtToken");
    if (!token) {
        window.location.href = "login.html";
        return;
    }

    const username = localStorage.getItem("username");
    const display = document.getElementById("userDisplay");
    if (username && display) {
        display.textContent = username;
        display.title = username;
    }

    loadGroups();
    getTasks();
    getRecentActivities();
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
            sidebar.classList.add('w-72');
        } else {
            sidebar.classList.add('w-0', 'p-0', 'border-none', 'overflow-hidden');
            sidebar.classList.remove('w-72');
        }
    }
}

function openRightPanel(type) {
    const panel = document.getElementById('rightPanel');
    const overlay = document.getElementById('rightPanelOverlay');
    const title = document.getElementById('rightPanelTitle');
    const friendsCont = document.getElementById('friendsContentContainer');
    const activitiesCont = document.getElementById('activitiesContentContainer');

    panel.classList.remove('translate-x-full');
    overlay.classList.remove('hidden');

    if (type === 'friends') {
        title.textContent = '👥 Arkadaşlarım';
        friendsCont.classList.remove('hidden');
        activitiesCont.classList.add('hidden');
        loadFriendsData();
    } else {
        title.textContent = '🔔 Son Hareketler';
        friendsCont.classList.add('hidden');
        activitiesCont.classList.remove('hidden');
        getRecentActivities();
    }
}

function closeRightPanel() {
    const panel = document.getElementById('rightPanel');
    const overlay = document.getElementById('rightPanelOverlay');
    panel.classList.add('translate-x-full');
    overlay.classList.add('hidden');
}

function openFriendModal() {
    document.getElementById('friendModal').classList.remove('hidden');
    document.getElementById('friendModalResult').innerHTML = '';
    document.getElementById('userSearchInput').value = '';
}

function closeFriendModal() {
    document.getElementById('friendModal').classList.add('hidden');
}

async function openAssignModal(taskId) {
    document.getElementById('assignModal').classList.remove('hidden');
    document.getElementById('assignTaskId').value = taskId;
    document.getElementById('assignModalResult').innerHTML = '';
    const select = document.getElementById('assignFriendSelect');
    select.innerHTML = '<option value="">Yükleniyor...</option>';

    try {
        const response = await fetch(`${apiUrl}/Friend/friendlist`, { headers: getHeaders() });
        if (response.ok) {
            const friends = await response.json();
            select.innerHTML = '<option value="">Arkadaş seçin...</option>';
            if (friends.length === 0) {
                select.innerHTML = '<option value="">Hiç arkadaşınız yok.</option>';
            } else {
                friends.forEach(f => {
                    const fId = f.friendId || f.FriendId;
                    const fName = f.friendName || f.FriendName;
                    select.innerHTML += `<option value="${fId}">@${fName}</option>`;
                });
            }
        }
    } catch (e) {
        select.innerHTML = '<option value="">Hata oluştu.</option>';
    }
}

function closeAssignModal() {
    document.getElementById('assignModal').classList.add('hidden');
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

async function addFriendByUsername() {
    const usernameInput = document.getElementById('userSearchInput');
    const resultDiv = document.getElementById('friendModalResult');
    const username = usernameInput.value.trim();

    if (!username) {
        resultDiv.innerHTML = '<span class="text-amber-500">Lütfen bir kullanıcı adı girin.</span>';
        return;
    }

    try {
        const response = await fetch(`${apiUrl}/Friend/add/${username}`, {
            method: "POST",
            headers: getHeaders()
        });

        if (response.ok) {
            const data = await response.json();
            resultDiv.innerHTML = `<span class="text-emerald-500">✅ ${data.message}</span>`;
            usernameInput.value = '';
            getRecentActivities();
            loadFriendsData();
        } else {
            const errorText = await response.text();
            resultDiv.innerHTML = `<span class="text-red-500">❌ ${errorText}</span>`;
        }
    } catch (error) {
        resultDiv.innerHTML = '<span class="text-red-500">❌ Bağlantı hatası.</span>';
    }
}

async function loadFriendsData() {
    try {
        const pendingRes = await fetch(`${apiUrl}/Friend/pending`, { headers: getHeaders() });
        const friendsRes = await fetch(`${apiUrl}/Friend/friendlist`, { headers: getHeaders() });

        if (pendingRes.ok && friendsRes.ok) {
            const pending = await pendingRes.json();
            const friends = await friendsRes.json();
            renderFriendsUI(pending, friends);
        }
    } catch (error) { console.error(error); }
}

function renderFriendsUI(pending, friends) {
    const pendingContainer = document.getElementById('pendingRequestsContainer');
    const friendsContainer = document.getElementById('friendList');

    pendingContainer.innerHTML = '';
    friendsContainer.innerHTML = '';

    if (pending.length > 0) {
        pendingContainer.classList.remove('hidden');
        pendingContainer.innerHTML = '<span class="text-[11px] font-bold text-amber-500 uppercase tracking-widest px-1">Gelen İstekler</span>';
        pending.forEach(req => {
            const reqId = req.requesterId || req.RequesterId;
            const reqName = req.requesterName || req.RequesterName;
            pendingContainer.innerHTML += `
                <div class="flex justify-between items-center bg-amber-50 dark:bg-amber-900/20 p-2 rounded-lg border border-amber-200 dark:border-amber-800 mt-1">
                    <div class="flex items-center gap-2">
                        <div class="w-6 h-6 rounded-full bg-amber-200 dark:bg-amber-700 flex items-center justify-center text-amber-800 dark:text-amber-100 text-[10px] font-bold shrink-0 leading-none">
                            ${reqName.charAt(0).toUpperCase()}
                        </div>
                        <span class="text-sm font-bold text-amber-700 dark:text-amber-400">@${reqName}</span>
                    </div>
                    <div class="flex gap-1 shrink-0">
                        <button onclick="acceptFriend(${reqId})" class="bg-emerald-500 hover:bg-emerald-600 text-white text-[10px] font-bold px-2 py-1 rounded transition-all">Kabul</button>
                        <button onclick="removeFriend(${reqId})" class="bg-red-500 hover:bg-red-600 text-white text-[10px] font-bold px-2 py-1 rounded transition-all">Red</button>
                    </div>
                </div>
            `;
        });
    } else {
        pendingContainer.classList.add('hidden');
    }

    if (friends.length > 0) {
        friends.forEach(f => {
            const fId = f.friendId || f.FriendId;
            const fName = f.friendName || f.FriendName;
            friendsContainer.innerHTML += `
                <div class="flex justify-between items-center bg-slate-50 dark:bg-[#252525] p-2 rounded-lg border border-slate-200 dark:border-slate-700">
                    <div class="flex items-center gap-2">
                        <div class="w-6 h-6 rounded-full bg-blue-100 dark:bg-blue-900/40 flex items-center justify-center text-blue-600 dark:text-blue-400 text-[10px] font-bold shrink-0 leading-none">
                            ${fName.charAt(0).toUpperCase()}
                        </div>
                        <span class="text-sm font-bold text-slate-700 dark:text-slate-300">@${fName}</span>
                    </div>
                    <button onclick="removeFriend(${fId})" class="text-red-500 hover:text-red-600 text-[10px] font-bold bg-red-50 dark:bg-red-900/20 px-2 py-1 rounded transition-all shrink-0">Çıkar</button>
                </div>
            `;
        });
    } else {
        friendsContainer.innerHTML = '<p class="text-xs text-slate-400 text-center py-4 border border-dashed border-slate-300 dark:border-slate-700 rounded-xl">Henüz arkadaşın yok.</p>';
    }
}

async function acceptFriend(id) {
    try {
        await fetch(`${apiUrl}/Friend/accept/${id}`, { method: "PUT", headers: getHeaders() });
        loadFriendsData();
        getRecentActivities();
    } catch (error) { console.error(error); }
}

async function removeFriend(id) {
    if (confirm("Bu işlemi onaylıyor musunuz?")) {
        try {
            await fetch(`${apiUrl}/Friend/remove/${id}`, { method: "DELETE", headers: getHeaders() });
            loadFriendsData();
            getRecentActivities();
        } catch (error) { console.error(error); }
    }
}

async function getRecentActivities() {
    try {
        const response = await fetch(`${apiUrl}/Tasks/recent-activities`, { method: "GET", headers: getHeaders() });
        if (response.ok) {
            const logs = await response.json();
            renderActivitiesUI(logs);
        }
    } catch (error) { console.error(error); }
}

function renderActivitiesUI(logs) {
    const container = document.getElementById('recentActivities');
    if (!container) return;
    container.innerHTML = '';

    if (!logs || logs.length === 0) {
        container.innerHTML = '<p class="text-xs text-slate-400 text-center py-4 border border-dashed border-slate-300 dark:border-slate-700 rounded-xl">Hareket bulunamadı.</p>';
        return;
    }

    logs.forEach(log => {
        const action = log.action || log.Action;
        const details = log.details || log.Details;
        container.innerHTML += `
            <div class="p-3 bg-slate-50 dark:bg-[#252525] rounded-xl border border-slate-200 dark:border-slate-700">
                <span class="block text-xs font-bold text-blue-500 mb-1">${action}</span>
                <span class="block text-xs text-slate-600 dark:text-slate-300">${details}</span>
            </div>
        `;
    });
}

async function confirmAssignUser() {
    const taskId = document.getElementById('assignTaskId').value;
    const friendId = document.getElementById('assignFriendSelect').value;
    const resultDiv = document.getElementById('assignModalResult');

    if (!friendId) {
        resultDiv.innerHTML = '<span class="text-amber-500">Lütfen bir kişi seçin.</span>';
        return;
    }

    try {
        const response = await fetch(`${apiUrl}/Tasks/${taskId}/assign/${friendId}`, {
            method: "POST",
            headers: getHeaders()
        });

        if (response.ok) {
            const data = await response.json();
            resultDiv.innerHTML = `<span class="text-emerald-500">✅ ${data.message}</span>`;
            getTasks();
            getRecentActivities();
            setTimeout(closeAssignModal, 1500);
        } else {
            const err = await response.text();
            resultDiv.innerHTML = `<span class="text-red-500">❌ ${err}</span>`;
        }
    } catch (error) {
        resultDiv.innerHTML = '<span class="text-red-500">❌ Bağlantı hatası.</span>';
    }
}

async function removeUser(taskId, userId, userName) {
    if (confirm(`@${userName} adlı kişiyi bu görevden çıkarmak (veya ayrılmak) istediğinize emin misiniz?`)) {
        try {
            const response = await fetch(`${apiUrl}/Tasks/${taskId}/assign/${userId}`, { method: "DELETE", headers: getHeaders() });
            if (response.ok) {
                getTasks();
                getRecentActivities();
            } else {
                const err = await response.text();
                alert(err);
            }
        } catch (error) { console.error(error); }
    }
}

async function loadGroups() {
    try {
        const res = await fetch(`${apiUrl}/Group/my-groups`, { headers: getHeaders() });
        if (res.ok) {
            myGroups = await res.json();
            renderGroupsSidebar();
            renderGroupsDropdown();
        }
    } catch (e) { console.error(e); }
}

function renderGroupsSidebar() {
    const container = document.getElementById('groupListSidebar');
    container.innerHTML = '';
    myGroups.forEach(g => {
        const gId = g.groupId || g.GroupId;
        const gName = g.name || g.Name;
        const isAdmin = g.isAdmin !== undefined ? g.isAdmin : g.IsAdmin;

        const btn = document.createElement('button');
        btn.className = `w-full flex items-center justify-between px-4 py-2.5 text-left rounded-xl text-slate-600 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-[#2c2c2c] transition-colors group-btn group-btn-${gId}`;
        btn.onclick = () => selectGroup(gId, gName, isAdmin);
        btn.innerHTML = `<div class="flex items-center gap-3 truncate"><span>📁</span> <span class="truncate">${gName}</span></div>`;
        container.appendChild(btn);
    });
}

function renderGroupsDropdown() {
    const select = document.getElementById('taskGroupSelect');
    select.innerHTML = '<option value="">👤 Kişisel Görev</option>';
    myGroups.forEach(g => {
        const gId = g.groupId || g.GroupId;
        const gName = g.name || g.Name;
        select.innerHTML += `<option value="${gId}">📁 ${gName}</option>`;
    });
    if (currentGroupId) {
        select.value = currentGroupId;
    }
}

function selectGroup(id, name, isAdmin) {
    currentGroupId = id;
    currentGroupIsAdmin = isAdmin;

    document.getElementById('pageTitle').textContent = name;
    document.getElementById('groupSettingsBtn').classList.remove('hidden');
    document.getElementById('taskGroupSelect').value = id;

    const navButtons = document.querySelectorAll('#sidebar button');
    navButtons.forEach(btn => {
        btn.classList.remove('group-active', 'bg-blue-50', 'dark:bg-blue-900/20', 'text-blue-600', 'dark:text-blue-400', 'font-medium');
        if (!btn.className.includes('text-left')) return;
        if (!btn.classList.contains('group-btn') && btn.id !== 'nav-personal') {
            btn.classList.add('text-slate-600', 'dark:text-slate-300');
        } else if (btn.classList.contains('group-btn')) {
            btn.classList.add('text-slate-600', 'dark:text-slate-300');
        }
    });

    const clickedBtn = document.querySelector(`.group-btn-${id}`);
    if (clickedBtn) {
        clickedBtn.classList.remove('text-slate-600', 'dark:text-slate-300');
        clickedBtn.classList.add('group-active', 'bg-blue-50', 'dark:bg-blue-900/20', 'text-blue-600', 'dark:text-blue-400', 'font-medium');
    }

    if (window.innerWidth < 768) toggleSidebar();

    getTasks();
}

function filterTasks(category) {
    currentGroupId = null;
    currentGroupIsAdmin = false;
    document.getElementById('groupSettingsBtn').classList.add('hidden');
    document.getElementById('taskGroupSelect').value = "";

    const title = document.getElementById('pageTitle');
    const sidebar = document.getElementById('sidebar');
    const overlay = document.getElementById('sidebarOverlay');

    document.querySelectorAll('#sidebar button[onclick^="filterTasks"]').forEach(btn => {
        btn.className = "w-full flex items-center gap-3 px-4 py-2.5 text-left rounded-xl text-slate-600 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-[#2c2c2c] transition-colors";
    });

    document.querySelectorAll('.group-btn').forEach(btn => {
        btn.classList.remove('group-active', 'bg-blue-50', 'dark:bg-blue-900/20', 'text-blue-600', 'dark:text-blue-400', 'font-medium');
        btn.classList.add('text-slate-600', 'dark:text-slate-300');
    });

    const activeBtn = document.querySelector(`#sidebar button[onclick="filterTasks('${category}', null)"]`) || document.querySelector(`#sidebar button[onclick="filterTasks('${category}')"]`) || document.getElementById('nav-personal');
    if (activeBtn) activeBtn.className = "w-full flex items-center gap-3 px-4 py-2.5 text-left rounded-xl bg-blue-50 dark:bg-blue-900/20 text-blue-600 dark:text-blue-400 font-medium hover:bg-blue-100 dark:hover:bg-blue-900/30 transition-colors group-active";

    if (window.innerWidth < 768) {
        if (sidebar) sidebar.classList.add('-translate-x-full');
        if (overlay) overlay.classList.add('hidden');
    }

    const filters = document.querySelectorAll('.filter-control');
    switch (category) {
        case 'all':
            title.textContent = "Kişisel Görevlerim";
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
    const pageTitle = document.getElementById('pageTitle') ? document.getElementById('pageTitle').textContent : "Kişisel Görevlerim";

    if (pageTitle.includes("Favoriler")) { fetchAndRender(`${apiUrl}/Tasks/favorites`); return; }
    if (pageTitle.includes("Çöp Kutusu")) { fetchAndRender(`${apiUrl}/Tasks/bin`); return; }

    let queryUrl = `${apiUrl}/Tasks?search=${searchVal}&status=${statusVal}`;
    if (priorityVal) queryUrl += `&priority=${priorityVal}`;
    if (currentGroupId) queryUrl += `&groupId=${currentGroupId}`;

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

async function toggleSubtask(event, taskId, lineIndex) {
    event.stopPropagation();
    const task = globalTasks.find(t => (t.id || t.Id || t.taskId || t.TaskId) === taskId);
    if (!task) return;

    let desc = task.description || task.Description || "";
    let lines = desc.split('\n');
    let trimmedLine = lines[lineIndex].trim();

    if (trimmedLine.startsWith('- [x]')) {
        lines[lineIndex] = lines[lineIndex].replace('- [x]', '-');
    } else if (trimmedLine.startsWith('-')) {
        lines[lineIndex] = lines[lineIndex].replace('-', '- [x]');
    }

    const newDesc = lines.join('\n');
    task.description = newDesc;
    if (task.Description !== undefined) task.Description = newDesc;

    const tTitle = task.title || task.Title;
    const tStatus = task.isCompleted !== undefined ? task.isCompleted : task.IsCompleted;
    const tFav = task.isFavorite === true || task.IsFavorite === true;
    const tPriority = task.priority !== undefined ? task.priority : task.Priority;
    const tDate = task.dueDate || task.DueDate;
    const tEventId = task.googleCalendarEventId || task.GoogleCalendarEventId || null;
    const tMsEventId = task.microsoftCalendarEventId || task.MicrosoftCalendarEventId || null;
    const tGroupId = task.groupId || task.GroupId || null;

    try {
        await fetch(`${apiUrl}/Tasks/${taskId}`, {
            method: "PUT",
            headers: getHeaders(),
            body: JSON.stringify({
                Id: taskId, Title: tTitle, Description: newDesc, IsCompleted: tStatus, isFavorite: tFav, Priority: tPriority, DueDate: tDate, GroupId: tGroupId, GoogleCalendarEventId: tEventId, MicrosoftCalendarEventId: tMsEventId
            })
        });
        getTasks();
    } catch (error) {
        console.error(error);
    }
}

function renderTasks(tasks) {
    globalTasks = tasks || [];
    const list = document.getElementById("taskList");
    if (!list) return;
    list.innerHTML = "";

    const searchVal = document.getElementById("searchInput")?.value || "";
    const statusVal = document.getElementById("statusFilter")?.value || "all";
    const priorityVal = document.getElementById("priorityFilter")?.value || "";
    const pageTitle = document.getElementById('pageTitle') ? document.getElementById('pageTitle').textContent : "Kişisel Görevlerim";

    const isBinPage = pageTitle.includes("Çöp Kutusu");
    const canDrag = !pageTitle.includes("Favoriler") && !isBinPage && searchVal === "" && statusVal === "all" && priorityVal === "";

    if (globalTasks.length === 0) {
        list.innerHTML = '<div class="text-center text-slate-500 py-10">Görev bulunamadı.</div>';
        updateSidebarStats(globalTasks);
        renderCalendar(globalTasks);
        return;
    }

    const now = new Date();

    globalTasks.forEach(task => {
        const tId = task.id || task.Id || task.taskId || task.TaskId;
        const tTitle = task.title || task.Title;
        let tDesc = task.description || task.Description || "";
        if (tDesc === "undefined" || tDesc === "null") tDesc = "";

        const tPriority = task.priority !== undefined ? task.priority : task.Priority;
        const tDate = task.dueDate || task.DueDate;
        const tIsCompleted = task.isCompleted !== undefined ? task.isCompleted : task.IsCompleted;
        const tIsFavorite = task.isFavorite === true || task.IsFavorite === true;

        const tIsDeleted = task.isDeleted === true || task.IsDeleted === true || isBinPage;
        const tGroupId = task.groupId || task.GroupId || null;

        const tAssignees = task.assignees || task.Assignees || task.assign || task.Assign || [];

        let priorityBadge = "";
        if (tPriority === 1) priorityBadge = `<span class="px-2 py-0.5 rounded text-xs font-bold bg-blue-100 text-blue-600">Düşük</span>`;
        else if (tPriority === 2) priorityBadge = `<span class="px-2 py-0.5 rounded text-xs font-bold bg-yellow-100 text-yellow-600">Orta</span>`;
        else if (tPriority === 3) priorityBadge = `<span class="px-2 py-0.5 rounded text-xs font-bold bg-red-100 text-red-600">Yüksek</span>`;

        let groupBadge = "";
        if (tGroupId && !currentGroupId) {
            const foundGroup = myGroups.find(g => (g.groupId || g.GroupId) === tGroupId);
            const gName = foundGroup ? (foundGroup.name || foundGroup.Name) : "Grup";
            groupBadge = `<span class="px-2 py-0.5 rounded text-xs font-bold bg-purple-100 text-purple-600 flex items-center gap-1">📁 ${gName}</span>`;
        }

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
            let dateText = `📅 ${dStr}`;

            if (!tIsCompleted) {
                if (isOverdue) {
                    dateColorClass = "text-red-600 dark:text-red-400 font-bold";
                    borderClass = "border-red-300 dark:border-red-900 border-2";
                    dateText = `📅 ${dStr} (Geçti)`;
                } else if (isUpcoming) {
                    dateColorClass = "text-amber-600 dark:text-amber-400 font-bold";
                }
            }
            dateBadge = `<span class="text-xs ${dateColorClass} flex items-center gap-1">${dateText}</span>`;
        }

        let assigneeHtml = "";
        if (!tGroupId && tAssignees.length > 0) {
            assigneeHtml = `<div class="flex flex-wrap space-x-0.5">`;
            tAssignees.forEach(a => {
                const uId = a.id || a.Id || a.userId || a.UserId;
                const name = a.username || a.Username || a.user?.username || a.User?.Username || a.user?.Username || a.User?.username || "A";

                assigneeHtml += `<div onclick="event.stopPropagation(); removeUser(${tId}, ${uId}, '${name}')" title="@${name} - Çıkarmak için tıkla" class="cursor-pointer w-7 h-7 rounded-full bg-blue-100 dark:bg-blue-900/40 border-2 border-white dark:border-[#252525] flex items-center justify-center text-[10px] font-bold text-blue-600 dark:text-blue-400 leading-none hover:bg-red-100 hover:text-red-600 dark:hover:bg-red-900 transition-colors shadow-sm">${name.charAt(0).toUpperCase()}</div>`;
            });
            assigneeHtml += `</div>`;
        }

        let formattedDescHtml = "";
        if (tDesc) {
            const lines = tDesc.split('\n');
            lines.forEach((line, index) => {
                const trimmed = line.trim();
                const isChecked = trimmed.startsWith('- [x]');
                const isUnchecked = trimmed.startsWith('- ') && !isChecked;

                if (isChecked || isUnchecked) {
                    const text = isChecked ? trimmed.substring(5).trim() : trimmed.substring(2).trim();
                    const checkState = isChecked ? "checked" : "";
                    const textClass = isChecked ? "line-through text-slate-400" : "text-slate-600 dark:text-slate-400";
                    formattedDescHtml += `
                        <div class="flex items-center gap-2 mt-1" onclick="event.stopPropagation()">
                            <input type="checkbox" ${checkState} onclick="toggleSubtask(event, ${tId}, ${index})" class="w-3.5 h-3.5 rounded border-slate-300 text-blue-600 focus:ring-blue-500 cursor-pointer">
                            <span class="text-xs ${textClass}">${text}</span>
                        </div>`;
                } else {
                    formattedDescHtml += `<span class="block text-sm text-slate-500 dark:text-slate-400 truncate mt-0.5">${line}</span>`;
                }
            });
        }

        const safeTitle = tTitle ? tTitle.replace(/'/g, "\\'") : "";
        const safeDescRaw = tDesc ? tDesc.replace(/'/g, "\\'").replace(/\n/g, "\\n") : "";
        const safeDate = tDate || "";
        const checkClass = tIsCompleted ? "bg-emerald-500 border-emerald-500 text-white" : "border-slate-300 text-transparent hover:border-emerald-500";
        const titleStyle = tIsCompleted ? "line-through text-slate-400" : "text-slate-900 dark:text-white";

        const starIcon = tIsFavorite ?
            `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" class="w-6 h-6 text-yellow-400"><path fill-rule="evenodd" d="M10.788 3.21c.448-1.077 1.976-1.077 2.424 0l2.082 5.007 5.404.433c1.164.093 1.636 1.545.749 2.305l-4.117 3.527 1.257 5.273c.271 1.136-.964 2.033-1.96 1.425L12 18.354 7.373 21.18c-.996.608-2.231-.29-1.96-1.425l1.257-5.273-4.117-3.527c-.887-.76-.415-2.212.749-2.305l5.404-.433 2.082-5.006z" clip-rule="evenodd" /></svg>` :
            `<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-6 h-6 text-slate-300 hover:text-yellow-400"><path stroke-linecap="round" stroke-linejoin="round" d="M11.48 3.499a.562.562 0 011.04 0l2.125 5.111a.563.563 0 00.475.345l5.518.442c.563.044.889.77.448 1.152l-4.204 3.614a.562.562 0 00-.182.557l1.285 5.385a.562.562 0 01-.84.61l-4.725-2.885a.563.563 0 00-.586 0L6.982 20.54a.562.562 0 01-.84-.61l1.285-5.386a.562.562 0 00-.182-.557l-4.204-3.614a.562.562 0 01.448-1.152l5.518-.442a.563.563 0 00.475-.345L11.48 3.5z" /></svg>`;

        const li = document.createElement("li");
        li.className = `task-item group flex items-start w-full p-4 mb-3 border rounded-xl shadow-sm transition-all ${canDrag ? 'cursor-move hover:scale-[1.005]' : 'cursor-default'} ${tIsCompleted ? 'bg-emerald-50/90 dark:bg-emerald-900/20 border-emerald-200' : `bg-white dark:bg-[#252525] ${borderClass}`}`;
        li.draggable = canDrag;
        li.dataset.id = tId;

        if (canDrag) {
            li.addEventListener('dragstart', () => li.classList.add('opacity-50', 'dragging', 'scale-105'));
            li.addEventListener('dragend', () => li.classList.remove('opacity-50', 'dragging', 'scale-105'));
        }

        li.innerHTML = `
            <button class="w-6 h-6 rounded-full border-2 flex items-center justify-center text-xs font-bold transition-all shrink-0 mt-0.5 ${checkClass}" 
                    onclick="event.stopPropagation(); toggleStatus(${tId})">
                ${tIsCompleted ? '✔' : ''}
            </button>
            <div class="flex-grow min-w-0 flex flex-col mx-4">
                <div class="flex items-center gap-2 mb-1">
                    ${priorityBadge}
                    ${groupBadge}
                    ${dateBadge}
                </div>
                <span class="block font-bold truncate text-lg ${titleStyle}">${tTitle}</span>
                <div class="mt-0.5">
                    ${formattedDescHtml}
                </div>
                <div class="flex items-center gap-1 mt-2">
                    ${assigneeHtml}
                    ${!tIsDeleted && !tGroupId ? `<button onclick="event.stopPropagation(); openAssignModal(${tId})" class="w-7 h-7 rounded-full bg-slate-50 dark:bg-slate-800 border border-dashed border-slate-300 dark:border-slate-600 flex items-center justify-center text-slate-500 hover:text-blue-500 hover:border-blue-500 transition-all text-sm shrink-0" title="Göreve Kişi Ekle">+</button>` : ''}
                </div>
            </div>
            <div class="flex items-center shrink-0 gap-1">
                ${!tIsDeleted ? `
                    <button class="p-1.5 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700 transition-all" onclick="event.stopPropagation(); toggleFavorite(${tId})">${starIcon}</button>
                    <div class="w-px h-6 bg-slate-200 dark:bg-slate-700 mx-1"></div>
                    <button class="p-1.5 rounded-lg text-amber-500 hover:bg-amber-50 dark:hover:bg-amber-900/20 transition-all" onclick="event.stopPropagation(); startEditMode(${tId})">✏️</button>
                    <button class="p-1.5 rounded-lg text-red-500 hover:bg-red-50 dark:hover:bg-red-900/20 transition-all" onclick="event.stopPropagation(); toggleBin(${tId})">🗑️</button>
                ` : `
                    <button class="px-3 py-1.5 text-blue-500 border border-blue-500 rounded-lg text-xs font-bold hover:bg-blue-500 hover:text-white transition-all" onclick="event.stopPropagation(); toggleBin(${tId})">GERİ YÜKLE</button>
                    <button class="px-3 py-1.5 text-red-500 border border-red-500 rounded-lg text-xs font-bold hover:bg-red-50 hover:text-white transition-all ml-2" onclick="event.stopPropagation(); deleteTask(${tId})">SİL</button>
                `}
            </div>
        `;
        list.appendChild(li);
    });
    if (canDrag) initDragAndDrop();
    updateSidebarStats(globalTasks);
    renderCalendar(globalTasks);
}

async function toggleFavorite(id) {
    try {
        const response = await fetch(`${apiUrl}/Tasks/favorites/${id}`, {
            method: "PUT",
            headers: getHeaders()
        });

        if (response.ok) {
            const data = await response.json();
            const newFavState = data.isFavorite !== undefined ? data.isFavorite : data.IsFavorite;

            const taskIndex = globalTasks.findIndex(t => (t.id || t.Id || t.taskId || t.TaskId) === id);
            if (taskIndex > -1) {
                globalTasks[taskIndex].isFavorite = newFavState;
                globalTasks[taskIndex].IsFavorite = newFavState;
            }

            const pageTitle = document.getElementById('pageTitle') ? document.getElementById('pageTitle').textContent : "";
            if (pageTitle.includes("Favoriler") && newFavState === false) {
                getTasks();
            } else {
                renderTasks(globalTasks);
            }
        }
    } catch (error) {
        console.error(error);
    }
}

async function toggleBin(id) {
    const taskIndex = globalTasks.findIndex(t => (t.id || t.Id || t.taskId || t.TaskId) === id);
    if (taskIndex > -1) {
        globalTasks.splice(taskIndex, 1);
        renderTasks(globalTasks);
    }

    try {
        const res = await fetch(`${apiUrl}/Tasks/bin/${id}`, { method: "PUT", headers: getHeaders() });
        if (!res.ok) {
            const err = await res.text();
            alert(`Hata: ${err}`);
            getTasks();
        }
    } catch (e) { console.error(e); getTasks(); }
}

async function deleteTask(id) {
    if (confirm("Kalıcı silinsin mi?")) {
        try {
            const res = await fetch(`${apiUrl}/Tasks/${id}`, { method: "DELETE", headers: getHeaders() });
            if (!res.ok) {
                const err = await res.text();
                alert(`Hata: ${err}`);
            }
            getTasks();
        } catch (e) { console.error(e); }
    }
}

let isEditing = false;
let editingId = null;
let editingStatus = false;
let editingEventId = null;
let editingMsEventId = null;

async function saveTask() {
    const titleInput = document.getElementById("taskTitle");
    const descInput = document.getElementById("taskDesc");
    const priorityInput = document.getElementById("taskPriority");
    const dateOnlyInput = document.getElementById("taskDateInput");
    const timeOnlyInput = document.getElementById("taskTimeInput");
    const groupSelect = document.getElementById("taskGroupSelect");

    if (!titleInput.value.trim()) {
        alert("Başlık girin!");
        return;
    }

    let finalDueDate = null;
    if (dateOnlyInput.value) {
        const timePart = timeOnlyInput.value ? timeOnlyInput.value : "00:00:00";
        finalDueDate = `${dateOnlyInput.value}T${timePart.length === 5 ? timePart + ":00" : timePart}`;
    }

    let currentFav = false;
    if (isEditing) {
        const task = globalTasks.find(t => (t.id || t.Id || t.taskId || t.TaskId) == editingId);
        if (task) {
            currentFav = task.isFavorite === true || task.IsFavorite === true;
        }
    }

    const taskData = {
        Title: titleInput.value,
        Description: descInput.value,
        Priority: parseInt(priorityInput.value) || 0,
        DueDate: finalDueDate,
        IsCompleted: isEditing ? editingStatus : false,
        isFavorite: currentFav,
        GroupId: groupSelect.value ? parseInt(groupSelect.value) : null,
        GoogleCalendarEventId: isEditing ? editingEventId : null,
        MicrosoftCalendarEventId: isEditing ? editingMsEventId : null
    };

    let url = isEditing ? `${apiUrl}/Tasks/${editingId}` : `${apiUrl}/Tasks`;
    let method = isEditing ? "PUT" : "POST";
    if (isEditing) taskData.Id = editingId;

    try {
        const response = await fetch(url, {
            method: method,
            headers: getHeaders(),
            body: JSON.stringify(taskData)
        });

        if (response.ok) {
            resetForm();
            getTasks();
            getRecentActivities();
        } else {
            const errorMsg = await response.text();
            alert(`Hata: ${errorMsg}`);
        }
    } catch (error) {
        console.error(error);
        alert("Sunucuya bağlanırken bir hata oluştu.");
    }
}

async function toggleStatus(id) {
    const taskIndex = globalTasks.findIndex(t => (t.id || t.Id || t.taskId || t.TaskId) === id);
    if (taskIndex === -1) return;

    const task = globalTasks[taskIndex];
    const currentStatus = task.isCompleted !== undefined ? task.isCompleted : task.IsCompleted;
    const newStatus = !currentStatus;

    task.isCompleted = newStatus;
    task.IsCompleted = newStatus;
    renderTasks(globalTasks);

    const title = task.title || task.Title;
    const desc = task.description || task.Description || "";
    const isFav = task.isFavorite === true || task.IsFavorite === true;
    const priority = task.priority !== undefined ? task.priority : task.Priority;
    const dueDate = task.dueDate || task.DueDate;
    const eventId = task.googleCalendarEventId || task.GoogleCalendarEventId || null;
    const msEventId = task.microsoftCalendarEventId || task.MicrosoftCalendarEventId || null;
    const tGroupId = task.groupId || task.GroupId || null;

    try {
        await fetch(`${apiUrl}/Tasks/${id}`, {
            method: "PUT",
            headers: getHeaders(),
            body: JSON.stringify({
                Id: id, Title: title, Description: desc, IsCompleted: newStatus, isFavorite: isFav, Priority: priority, DueDate: dueDate && dueDate !== "null" ? dueDate : null, GroupId: tGroupId, GoogleCalendarEventId: eventId, MicrosoftCalendarEventId: msEventId
            })
        });
        getRecentActivities();
    } catch (e) {
        console.error(e);
    }
}

function startEditMode(id) {
    const task = globalTasks.find(t => (t.id || t.Id || t.taskId || t.TaskId) === id);
    if (!task) return;

    editingEventId = task.googleCalendarEventId || task.GoogleCalendarEventId || null;
    editingMsEventId = task.microsoftCalendarEventId || task.MicrosoftCalendarEventId || null;
    const tPriority = task.priority !== undefined ? task.priority : (task.Priority || 2);

    isEditing = true;
    editingId = id;
    editingStatus = task.isCompleted !== undefined ? task.isCompleted : task.IsCompleted;

    document.getElementById("taskTitle").value = task.title || task.Title;
    const desc = task.description || task.Description;
    document.getElementById("taskDesc").value = (desc === "undefined" || !desc) ? "" : desc;
    document.getElementById("taskPriority").value = tPriority;

    const dueDate = task.dueDate || task.DueDate;
    if (dueDate && dueDate !== "null") {
        const parts = dueDate.split('T');
        document.getElementById("taskDateInput").value = parts[0];
        if (parts[1]) document.getElementById("taskTimeInput").value = parts[1].substring(0, 5);
    } else {
        document.getElementById("taskDateInput").value = "";
        document.getElementById("taskTimeInput").value = "";
    }

    document.getElementById("taskGroupSelect").value = task.groupId || task.GroupId || "";

    const saveBtn = document.getElementById("saveBtn");
    const cancelBtn = document.getElementById("cancelBtn");
    saveBtn.textContent = "GÜNCELLE";
    saveBtn.classList.replace("bg-blue-600", "bg-amber-500");
    cancelBtn.classList.remove("hidden");
    document.getElementById("taskTitle").focus();
}

function resetForm() {
    isEditing = false;
    editingId = null;
    editingEventId = null;
    editingMsEventId = null;
    document.getElementById("taskTitle").value = "";
    document.getElementById("taskDesc").value = "";
    document.getElementById("taskPriority").value = "2";
    document.getElementById("taskDateInput").value = "";
    document.getElementById("taskTimeInput").value = "";
    if (currentGroupId) {
        document.getElementById("taskGroupSelect").value = currentGroupId;
    } else {
        document.getElementById("taskGroupSelect").value = "";
    }
    const saveBtn = document.getElementById("saveBtn");
    const cancelBtn = document.getElementById("cancelBtn");
    saveBtn.textContent = "EKLE";
    saveBtn.classList.replace("bg-amber-500", "bg-blue-600");
    cancelBtn.classList.add("hidden");
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

function updateSidebarStats(tasks) {
    const textEl = document.getElementById('successRateText');
    const barEl = document.getElementById('successRateBar');
    if (!textEl || !barEl) return;
    const activeTasks = tasks.filter(t => {
        const isDel = t.isDeleted === true || t.IsDeleted === true;
        return !isDel;
    });
    const total = activeTasks.length;
    if (total === 0) { textEl.innerText = "0%"; barEl.style.width = "0%"; return; }
    const completed = activeTasks.filter(t => t.isCompleted || t.IsCompleted).length;
    const rate = Math.round((completed / total) * 100);
    textEl.innerText = `%${rate}`;
    barEl.style.width = `${rate}%`;
}

let currentCalendarDate = new Date();

function renderCalendar(tasks) {
    const calContainer = document.getElementById('miniCalendar');
    if (!calContainer) return;

    const year = currentCalendarDate.getFullYear();
    const month = currentCalendarDate.getMonth();
    const today = new Date();

    const monthNames = ["Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"];
    const dayNames = ["Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz"];

    const firstDayOfMonth = new Date(year, month, 1).getDay();
    const adjustedFirstDay = firstDayOfMonth === 0 ? 6 : firstDayOfMonth - 1;
    const daysInMonth = new Date(year, month + 1, 0).getDate();

    const prevMonthLastDay = new Date(year, month, 0).getDate();

    const taskCounts = {};
    tasks.forEach(t => {
        const dateStr = t.dueDate || t.DueDate;
        const isDel = t.isDeleted === true || t.IsDeleted === true;
        if (dateStr && !isDel) {
            const d = new Date(dateStr);
            if (d.getFullYear() === year && d.getMonth() === month) {
                const day = d.getDate();
                taskCounts[day] = (taskCounts[day] || 0) + 1;
            }
        }
    });

    let html = `
        <div class="flex justify-between items-center mb-3 px-1">
            <span class="text-sm font-bold text-slate-800 dark:text-white">${monthNames[month]} ${year}</span>
            <div class="flex gap-1">
                <button onclick="changeMonth(-1)" class="p-1 hover:bg-slate-200 dark:hover:bg-slate-700 rounded-md transition-colors text-slate-500">❮</button>
                <button onclick="changeMonth(1)" class="p-1 hover:bg-slate-200 dark:hover:bg-slate-700 rounded-md transition-colors text-slate-500">❯</button>
            </div>
        </div>
        <div class="grid grid-cols-7 gap-1 text-center mb-2">
            ${dayNames.map(d => `<div class="text-[10px] font-bold text-slate-400">${d}</div>`).join('')}
        </div>
        <div class="grid grid-cols-7 gap-1 text-center">`;

    for (let i = adjustedFirstDay; i > 0; i--) {
        const prevDay = prevMonthLastDay - i + 1;
        html += `<div class="text-xs p-1.5 rounded-md flex flex-col items-center justify-center h-8 text-slate-300 dark:text-slate-600 cursor-not-allowed"><span>${prevDay}</span></div>`;
    }

    for (let i = 1; i <= daysInMonth; i++) {
        const isToday = i === today.getDate() && month === today.getMonth() && year === today.getFullYear();
        const count = taskCounts[i] || 0;
        const baseClass = "relative text-xs p-1.5 rounded-md flex flex-col items-center justify-center transition-colors h-8 cursor-pointer";
        const activeClass = isToday ? "bg-blue-500 text-white font-bold" : "text-slate-600 dark:text-slate-300 hover:bg-slate-200 dark:hover:bg-slate-600";
        let indicator = count > 0 ? `<div class="absolute bottom-1 w-1 h-1 ${isToday ? 'bg-white' : 'bg-blue-500'} rounded-full"></div>` : "";
        html += `<div class="${baseClass} ${activeClass}" onclick="openDailyModal(${year}, ${month}, ${i})"><span>${i}</span>${indicator}</div>`;
    }

    const totalCells = adjustedFirstDay + daysInMonth;
    const nextMonthDays = totalCells % 7 === 0 ? 0 : 7 - (totalCells % 7);
    for (let i = 1; i <= nextMonthDays; i++) {
        html += `<div class="text-xs p-1.5 rounded-md flex flex-col items-center justify-center h-8 text-slate-300 dark:text-slate-600 cursor-not-allowed"><span>${i}</span></div>`;
    }

    html += `</div>`;
    calContainer.innerHTML = html;
}

function changeMonth(offset) {
    currentCalendarDate.setMonth(currentCalendarDate.getMonth() + offset);
    getTasks();
}

function openDailyModal(year, month, day) {
    const dateObj = new Date(year, month, day);
    const options = { day: 'numeric', month: 'long', year: 'numeric', weekday: 'long' };
    const titleDate = dateObj.toLocaleDateString('tr-TR', options);
    document.getElementById('dailyModalTitle').innerHTML = `📅 ${titleDate}`;

    const content = document.getElementById('dailyModalContent');
    content.innerHTML = '';

    const dayTasks = globalTasks.filter(t => {
        const isDel = t.isDeleted === true || t.IsDeleted === true;
        if (isDel) return false;
        const dateStr = t.dueDate || t.DueDate;
        if (!dateStr) return false;
        const d = new Date(dateStr);
        return d.getFullYear() === year && d.getMonth() === month && d.getDate() === day;
    });

    if (dayTasks.length === 0) {
        content.innerHTML = '<p class="text-center text-slate-500 py-6">Bu tarihte görev bulunmuyor.</p>';
    } else {
        dayTasks.forEach(task => {
            const tTitle = task.title || task.Title;
            const isCompleted = task.isCompleted || task.IsCompleted;
            const checkClass = isCompleted ? "bg-emerald-500 border-emerald-500 text-white" : "border-slate-300 text-transparent";
            const titleStyle = isCompleted ? "line-through text-slate-400" : "text-slate-900 dark:text-white";

            content.innerHTML += `
                <div class="flex items-center gap-3 p-3 mb-2 border border-slate-200 dark:border-slate-700 rounded-xl bg-slate-50 dark:bg-[#252525]">
                    <div class="w-5 h-5 rounded-full border-2 flex items-center justify-center text-[10px] font-bold shrink-0 ${checkClass}">${isCompleted ? '✔' : ''}</div>
                    <span class="font-bold text-sm ${titleStyle}">${tTitle}</span>
                </div>
            `;
        });
    }
    document.getElementById('dailyModal').classList.remove('hidden');
}

function closeDailyModal() {
    document.getElementById('dailyModal').classList.add('hidden');
}

async function deleteAccount() {
    const confirmText = prompt("Hesabınızı kalıcı olarak silmek istediğinize emin misiniz? Bu işlem geri alınamaz. Onaylamak için 'SİL' yazın:");
    if (confirmText === "SİL") {
        try {
            const response = await fetch(`${apiUrl}/Auth/delete`, {
                method: "DELETE",
                headers: getHeaders()
            });
            if (response.ok) {
                alert("Hesabınız başarıyla silindi.");
                localStorage.removeItem("jwtToken");
                localStorage.removeItem("username");
                window.location.href = "login.html";
            } else {
                const errText = await response.text();
                alert(`Hata: ${errText}`);
            }
        } catch (error) {
            console.error(error);
            alert("Bağlantı hatası.");
        }
    } else if (confirmText !== null) {
        alert("Hatalı giriş yaptınız, silme işlemi iptal edildi.");
    }
}

function openCreateGroupModal() {
    document.getElementById('createGroupModal').classList.remove('hidden');
    document.getElementById('groupNameInput').value = '';
    document.getElementById('groupDescInput').value = '';
}

function closeCreateGroupModal() {
    document.getElementById('createGroupModal').classList.add('hidden');
}

async function submitCreateGroup() {
    const name = document.getElementById('groupNameInput').value.trim();
    const desc = document.getElementById('groupDescInput').value.trim();
    if (!name) { alert("Grup adı zorunludur."); return; }

    try {
        const response = await fetch(`${apiUrl}/Group/create`, {
            method: "POST",
            headers: getHeaders(),
            body: JSON.stringify({ Name: name, Description: desc })
        });

        if (response.ok) {
            closeCreateGroupModal();
            loadGroups();
            getRecentActivities();
        } else {
            const err = await response.text();
            alert(`Hata: ${err}`);
        }
    } catch (e) { console.error(e); }
}

async function openGroupSettings() {
    if (!currentGroupId) return;

    const group = myGroups.find(g => (g.groupId || g.GroupId) === currentGroupId);
    if (!group) return;

    document.getElementById('settingsGroupName').textContent = group.name || group.Name;
    document.getElementById('settingsGroupDesc').textContent = group.description || group.Description;

    const delBtn = document.getElementById('deleteGroupBtn');
    const isAdmin = group.isAdmin !== undefined ? group.isAdmin : group.IsAdmin;

    if (isAdmin) {
        delBtn.classList.remove('hidden');
    } else {
        delBtn.classList.add('hidden');
    }

    const select = document.getElementById('groupAddMemberSelect');
    select.innerHTML = '<option value="">Gruba eklenecek arkadaşını seç...</option>';
    const membersList = document.getElementById('groupMembersList');
    membersList.innerHTML = '<div class="text-center text-slate-500 py-4">Üyeler yükleniyor...</div>';

    try {
        const response = await fetch(`${apiUrl}/Friend/friendlist`, { headers: getHeaders() });
        if (response.ok) {
            const friends = await response.json();
            friends.forEach(f => {
                const fName = f.friendName || f.FriendName;
                select.innerHTML += `<option value="${fName}">@${fName}</option>`;
            });
        }
    } catch (e) {
        console.error(e);
    }

    document.getElementById('groupSettingsModal').classList.remove('hidden');

    try {
        const memRes = await fetch(`${apiUrl}/Group/${currentGroupId}/members`, { headers: getHeaders() });
        if (memRes.ok) {
            const members = await memRes.json();
            membersList.innerHTML = '';
            const myUsername = localStorage.getItem("username");

            members.forEach(m => {
                const uId = m.userId || m.UserId;
                const uName = m.username || m.Username;
                const isMemAdmin = m.isAdmin !== undefined ? m.isAdmin : m.IsAdmin;

                let actionsHtml = '';
                if (isAdmin && uName !== myUsername) {
                    actionsHtml = `
                        <div class="flex gap-2">
                            <button onclick="toggleGroupAdmin(${uId})" class="text-[10px] font-bold px-2 py-1 rounded ${isMemAdmin ? 'bg-amber-100 text-amber-700 hover:bg-amber-200' : 'bg-blue-100 text-blue-700 hover:bg-blue-200'} transition-all">${isMemAdmin ? 'Yetkiyi Al' : 'Admin Yap'}</button>
                            <button onclick="kickGroupMember(${uId}, '${uName}')" class="text-[10px] font-bold px-2 py-1 rounded bg-red-100 text-red-700 hover:bg-red-200 transition-all">Çıkar</button>
                        </div>
                    `;
                }

                membersList.innerHTML += `
                    <div class="flex justify-between items-center p-2 bg-white dark:bg-[#1e1e1e] border border-slate-200 dark:border-slate-700 rounded-lg mb-2">
                        <div class="flex items-center gap-2">
                            <div class="w-6 h-6 rounded-full bg-slate-200 dark:bg-slate-700 flex items-center justify-center text-[10px] font-bold text-slate-600 dark:text-slate-300">${uName.charAt(0).toUpperCase()}</div>
                            <span class="text-sm font-bold text-slate-700 dark:text-slate-300">@${uName} ${isMemAdmin ? '👑' : ''}</span>
                        </div>
                        ${actionsHtml}
                    </div>
                `;
            });
        } else {
            membersList.innerHTML = '<div class="text-center text-red-500 py-4">Üyeleri görme yetkiniz yok.</div>';
        }
    } catch (e) {
        console.error(e);
        membersList.innerHTML = '<div class="text-center text-red-500 py-4">Üyeler yüklenemedi.</div>';
    }
}

function closeGroupSettingsModal() {
    document.getElementById('groupSettingsModal').classList.add('hidden');
}

async function addMemberToGroup() {
    const username = document.getElementById('groupAddMemberSelect').value;
    if (!username) { alert("Lütfen bir arkadaşınızı seçin."); return; }

    try {
        const response = await fetch(`${apiUrl}/Group/${currentGroupId}/add-member/${username}`, {
            method: "POST",
            headers: getHeaders()
        });

        if (response.ok) {
            alert("Arkadaşınız gruba eklendi.");
            closeGroupSettingsModal();
            loadGroups();
            setTimeout(openGroupSettings, 500);
        } else {
            const err = await response.text();
            alert(`Hata: ${err}`);
        }
    } catch (e) { console.error(e); }
}

async function leaveGroup() {
    if (!confirm("Bu gruptan ayrılmak istediğinize emin misiniz?")) return;

    try {
        const response = await fetch(`${apiUrl}/Group/${currentGroupId}/leave`, {
            method: "DELETE",
            headers: getHeaders()
        });

        if (response.ok) {
            closeGroupSettingsModal();
            filterTasks('all');
            loadGroups();
        } else {
            const err = await response.text();
            alert(`Hata: ${err}`);
        }
    } catch (e) { console.error(e); }
}

async function deleteGroup() {
    if (!confirm("Bu grubu tamamen silmek istediğinize emin misiniz? Tüm görevler silinecektir!")) return;

    try {
        const response = await fetch(`${apiUrl}/Group/${currentGroupId}/delete`, {
            method: "DELETE",
            headers: getHeaders()
        });

        if (response.ok) {
            closeGroupSettingsModal();
            filterTasks('all');
            loadGroups();
        } else {
            const err = await response.text();
            alert(`Hata: ${err}`);
        }
    } catch (e) { console.error(e); }
}

async function toggleGroupAdmin(userId) {
    try {
        const res = await fetch(`${apiUrl}/Group/${currentGroupId}/toggle-admin/${userId}`, { method: "PUT", headers: getHeaders() });
        if (res.ok) {
            openGroupSettings();
        } else {
            const err = await res.text();
            alert(`Hata: ${err}`);
        }
    } catch (e) { console.error(e); }
}

async function kickGroupMember(userId, userName) {
    if (!confirm(`@${userName} adlı kullanıcıyı gruptan çıkarmak istediğinize emin misiniz?`)) return;
    try {
        const res = await fetch(`${apiUrl}/Group/${currentGroupId}/remove-member/${userId}`, { method: "POST", headers: getHeaders() });
        if (res.ok) {
            openGroupSettings();
        } else {
            const err = await res.text();
            alert(`Hata: ${err}`);
        }
    } catch (e) { console.error(e); }
}