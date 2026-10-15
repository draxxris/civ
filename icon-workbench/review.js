const picker = document.querySelector("#batch-picker");
const filterInput = document.querySelector("#filter-input");
const cards = document.querySelector("#cards");
const form = document.querySelector("#review-form");
const batchValue = document.querySelector("#batch-value");
const saveButton = document.querySelector("#save-button");
const summary = document.querySelector("#batch-summary");
const message = document.querySelector("#page-message");
const largeToggle = document.querySelector("#large-preview-toggle");
const hideApprovedToggle = document.querySelector("#hide-approved-toggle");

let currentBatch = null;
let currentCells = [];
let dirty = false;

function setMessage(text, isError = false) {
  message.textContent = text;
  message.classList.toggle("error", isError);
}

async function getJson(url, options) {
  const response = await fetch(url, options);
  const payload = await response.json();
  if (!response.ok) {
    throw new Error(payload.error || `Request failed with status ${response.status}`);
  }
  return payload;
}

function addText(parent, tag, className, text) {
  const element = document.createElement(tag);
  if (className) element.className = className;
  element.textContent = text;
  parent.append(element);
  return element;
}

function makePreview(caption, url, label) {
  const figure = document.createElement("figure");
  figure.className = "preview";
  addText(figure, "figcaption", "", caption);
  const image = document.createElement("img");
  image.src = url;
  image.alt = `${caption} icon for ${label}`;
  image.width = 64;
  image.height = 64;
  image.loading = "lazy";
  figure.append(image);
  return figure;
}

function makeRejectionField(cell) {
  const label = document.createElement("label");
  label.className = "rejection-control";
  label.hidden = !cell.rejected;
  const noteLabel = cell.revised_candidate && cell.rejected
    ? "Prior rejection note (required)"
    : "Rejection note (required)";
  addText(label, "span", "", noteLabel);
  const input = document.createElement("textarea");
  input.className = "rejection-note";
  input.name = "rejection-note";
  input.maxLength = 1000;
  input.rows = 3;
  input.required = cell.rejected;
  input.value = cell.rejection_note;
  input.placeholder = "Describe what is wrong and what to regenerate.";
  label.append(input);
  return label;
}

function makeCard(cell, index) {
  const card = document.createElement("article");
  card.className = "card";
  card.dataset.search = `${cell.label} ${cell.catalog_id} ${cell.subject} ${cell.row} ${cell.column}`.toLocaleLowerCase();
  card.dataset.savedStatus = cell.approved ? "approved" : cell.rejected ? "rejected" : "unreviewed";
  card.dataset.changed = "false";

  const heading = document.createElement("div");
  heading.className = "card-heading";
  const titleGroup = document.createElement("div");
  addText(titleGroup, "h2", "", cell.label);
  addText(titleGroup, "p", "card-meta", cell.catalog_id);
  addText(titleGroup, "p", "card-meta", `${currentBatch} · row ${cell.row} · column ${cell.column}`);
  if (cell.revised_candidate) {
    const details = [cell.revision_badge, cell.revision].filter(Boolean).join(" · ");
    addText(
      titleGroup,
      "p",
      "revision-notice",
      `${cell.revision_text || "Revised candidate"} · review required${details ? ` · ${details}` : ""}`,
    );
  }
  heading.append(titleGroup);
  const badge = addText(heading, "span", "state-badge", "Unreviewed");
  badge.dataset.state = "badge";
  card.append(heading);

  const comparison = document.createElement("div");
  comparison.className = "comparison";
  comparison.append(makePreview("Original · 64×64", cell.original_url, cell.label));
  const candidateCaption = cell.revised_candidate
    ? "Revised candidate · 64×64"
    : "Replacement · 64×64";
  comparison.append(makePreview(candidateCaption, cell.candidate_url, cell.label));
  card.append(comparison);
  addText(card, "p", "subject", `Intended subject: ${cell.subject}`);

  const approval = document.createElement("label");
  approval.className = "approval-control";
  const checkbox = document.createElement("input");
  checkbox.type = "checkbox";
  checkbox.name = "approved";
  checkbox.value = cell.catalog_id;
  checkbox.className = "approval-toggle";
  checkbox.id = `approval-${currentBatch}-${index}`;
  checkbox.checked = !cell.rejected;
  approval.append(checkbox);
  addText(approval, "span", "", "Approve this replacement");
  card.append(approval, makeRejectionField(cell));
  updateCardState(card);
  return card;
}

function updateCardState(card) {
  const checkbox = card.querySelector(".approval-toggle");
  const noteLabel = card.querySelector(".rejection-control");
  const note = card.querySelector(".rejection-note");
  const badge = card.querySelector('[data-state="badge"]');
  const changed = card.dataset.changed === "true";
  const savedStatus = card.dataset.savedStatus;

  noteLabel.hidden = checkbox.checked;
  note.required = !checkbox.checked;
  card.classList.toggle("approved", !changed && checkbox.checked && savedStatus === "approved");
  card.classList.toggle("rejected", !changed && !checkbox.checked && savedStatus === "rejected");
  card.classList.toggle("pending", changed);

  if (!changed && savedStatus === "approved") badge.textContent = "Approved";
  else if (!changed && savedStatus === "rejected") badge.textContent = "Rejected";
  else if (!changed) badge.textContent = "Unreviewed · pre-checked";
  else if (checkbox.checked) badge.textContent = "Pending approval";
  else badge.textContent = note.value.trim() ? "Pending rejection" : "Rejection note required";
}

function updateSummary() {
  const approvedCount = cards.querySelectorAll(".approval-toggle:checked").length;
  const rejectedCount = currentCells.length - approvedCount;
  summary.textContent = `${currentBatch}: ${currentCells.length} candidates · ${approvedCount} selected to approve · ${rejectedCount} selected to reject`;
}

function renderReview(review) {
  currentBatch = review.batch;
  currentCells = review.cells;
  batchValue.value = currentBatch;
  picker.value = currentBatch;
  cards.replaceChildren(...currentCells.map(makeCard));
  cards.classList.toggle("large-previews", largeToggle.checked);
  dirty = false;
  saveButton.disabled = false;
  filterInput.value = "";
  filterCards();
  updateSummary();
  setMessage(review.approved_count || review.rejected_count
    ? `${review.approved_count} approvals and ${review.rejected_count} rejections loaded for this batch.`
    : "This batch is unreviewed. All items start checked.");
  const url = new URL(window.location.href);
  url.searchParams.set("batch", currentBatch);
  window.history.replaceState({}, "", url);
}

function filterCards() {
  const query = filterInput.value.trim().toLocaleLowerCase();
  const hideApproved = hideApprovedToggle.checked;
  for (const card of cards.children) {
    const matchesSearch = query === "" || card.dataset.search.includes(query);
    const isSavedApproved = card.dataset.savedStatus === "approved";
    card.hidden = !matchesSearch || (hideApproved && isSavedApproved);
  }
}

function markCardChanged(card) {
  card.dataset.changed = "true";
  dirty = true;
  updateCardState(card);
}

async function loadReview(batchName) {
  saveButton.disabled = true;
  cards.replaceChildren();
  setMessage("Loading batch…");
  try {
    const review = await getJson(`/api/review?batch=${encodeURIComponent(batchName)}`);
    renderReview(review);
  } catch (error) {
    setMessage(error.message, true);
    summary.textContent = "The batch did not load.";
  }
}

async function loadBatches() {
  const result = await getJson("/api/batches");
  picker.replaceChildren();
  for (const batch of result.batches) {
    const option = document.createElement("option");
    option.value = batch.name;
    option.textContent = `${batch.name} · ${batch.approved_count} approved · ${batch.rejected_count} rejected`;
    picker.append(option);
  }
  picker.disabled = false;

  const requested = new URLSearchParams(window.location.search).get("batch");
  const initial = result.batches.some((batch) => batch.name === requested)
    ? requested
    : result.batches[0]?.name;
  if (!initial) throw new Error("No review batches are available");
  await loadReview(initial);
}

function collectSubmission() {
  const approved = [];
  const rejected = [];
  for (const card of cards.querySelectorAll(".card")) {
    const checkbox = card.querySelector(".approval-toggle");
    if (checkbox.checked) {
      approved.push(checkbox.value);
      continue;
    }

    const noteInput = card.querySelector(".rejection-note");
    const note = noteInput.value.trim();
    if (!note) {
      card.hidden = false;
      noteInput.focus();
      setMessage(`Enter a rejection note for ${checkbox.value}.`, true);
      return null;
    }
    rejected.push({ catalog_id: checkbox.value, note });
  }
  return { approved, rejected };
}

picker.addEventListener("change", async () => {
  const nextBatch = picker.value;
  if (dirty && !window.confirm("Discard unsaved decisions for this batch?")) {
    picker.value = currentBatch;
    return;
  }
  await loadReview(nextBatch);
});

filterInput.addEventListener("input", filterCards);
hideApprovedToggle.addEventListener("change", filterCards);

largeToggle.addEventListener("change", () => {
  cards.classList.toggle("large-previews", largeToggle.checked);
});

form.addEventListener("change", (event) => {
  if (!event.target.matches(".approval-toggle")) return;
  markCardChanged(event.target.closest(".card"));
  updateSummary();
  setMessage("Unsaved decisions. Checked items will be approved when saved.");
});

form.addEventListener("input", (event) => {
  if (!event.target.matches(".rejection-note")) return;
  markCardChanged(event.target.closest(".card"));
});

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  if (!currentBatch) return;
  const decisions = collectSubmission();
  if (!decisions) return;

  saveButton.disabled = true;
  setMessage("Saving decisions…");
  try {
    const result = await getJson("/api/selections", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ batch: currentBatch, ...decisions }),
    });
    dirty = false;
    const option = picker.selectedOptions[0];
    option.textContent = `${currentBatch} · ${result.approved_count} approved · ${result.rejected_count} rejected`;
    try {
      const review = await getJson(`/api/review?batch=${encodeURIComponent(currentBatch)}`);
      renderReview(review);
      setMessage(`Saved ${result.approved_count} approvals and ${result.rejected_count} rejection notes for ${currentBatch}. Other batches are unchanged.`);
    } catch (error) {
      setMessage(`Decisions saved. The page could not reload them: ${error.message}`, true);
    }
  } catch (error) {
    setMessage(error.message, true);
  } finally {
    saveButton.disabled = false;
  }
});

window.addEventListener("beforeunload", (event) => {
  if (!dirty) return;
  event.preventDefault();
  event.returnValue = "";
});

loadBatches().catch((error) => {
  picker.disabled = true;
  saveButton.disabled = true;
  setMessage(error.message, true);
  summary.textContent = "The review data did not load.";
});
