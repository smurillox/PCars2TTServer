const filters = ['track', 'vehicle', 'vehicleClass', 'gamertag'];
const state = Object.fromEntries(filters.map((key) => [key, '']));
const rows = document.querySelector('#rows');
const count = document.querySelector('#count');
const title = document.querySelector('#result-title');
const error = document.querySelector('#error');

const escapeHtml = (value) => String(value ?? '').replace(/[&<>'"]/g, (char) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[char]));
const formatLap = (milliseconds) => {
  const minutes = Math.floor(milliseconds / 60000);
  const seconds = ((milliseconds % 60000) / 1000).toFixed(3).padStart(6, '0');
  return `${minutes}:${seconds}`;
};
const formatDate = (value) => value ? new Date(value).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' }) : '—';

async function getJson(url) {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`Request failed (${response.status})`);
  return response.json();
}

function populateSelect(id, values, emptyLabel) {
  const select = document.querySelector(`#${id}`);
  select.innerHTML = `<option value="">${emptyLabel}</option>` + values.map((value) => `<option value="${escapeHtml(value)}">${escapeHtml(value)}</option>`).join('');
  select.value = state[id];
}

function render(records) {
  count.textContent = records.length;
  title.textContent = filters.some((key) => state[key]) ? 'Filtered records' : 'All records';
  if (!records.length) {
    rows.innerHTML = '<tr><td colspan="7" class="empty">No records match these filters.</td></tr>';
    return;
  }
  rows.innerHTML = records.map((record, index) => `<tr>
    <td>${String(index + 1).padStart(2, '0')}</td>
    <td><strong>${escapeHtml(record.gamertag)}</strong></td>
    <td>${escapeHtml(record.vehicle)}</td>
    <td>${escapeHtml(record.vehicleClass)}</td>
    <td>${escapeHtml(record.track)}</td>
    <td>${formatLap(record.lapTimeMilliseconds)}</td>
    <td>${formatDate(record.lapDate)}</td>
  </tr>`).join('');
}

async function loadRecords() {
  error.hidden = true;
  rows.innerHTML = '<tr><td colspan="7" class="empty">Loading records...</td></tr>';
  const params = new URLSearchParams({ limit: '1000' });
  filters.forEach((key) => { if (state[key]) params.set(key, state[key]); });
  try { render(await getJson(`/api/laps?${params}`)); }
  catch (exception) { error.textContent = exception.message; error.hidden = false; rows.innerHTML = '<tr><td colspan="7" class="empty">Could not load records.</td></tr>'; }
}

async function loadOptions() {
  try {
    const options = await getJson('/api/laps/options');
    populateSelect('track', options.tracks, 'All tracks');
    populateSelect('vehicle', options.vehicles, 'All vehicles');
    populateSelect('vehicleClass', options.vehicleClasses, 'All classes');
    populateSelect('gamertag', options.gamertags, 'All drivers');
  } catch (exception) { error.textContent = exception.message; error.hidden = false; }
}

filters.forEach((key) => document.querySelector(`#${key}`).addEventListener('change', (event) => { state[key] = event.target.value; loadRecords(); }));
document.querySelector('#reset').addEventListener('click', () => { filters.forEach((key) => { state[key] = ''; document.querySelector(`#${key}`).value = ''; }); loadRecords(); });
document.querySelector('#refresh').addEventListener('click', () => { loadOptions(); loadRecords(); });
loadOptions();
loadRecords();
