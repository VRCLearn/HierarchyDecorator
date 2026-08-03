const copyButton = document.querySelector("#copy-url");
const addButton = document.querySelector("#add-to-vcc");
const status = document.querySelector("#copy-status");
const repositoryUrl = document.querySelector("#repository-url");
const packageGrid = document.querySelector("#package-grid");
let listingUrl = "";

function createDefinition(label, value) {
    const wrapper = document.createElement("div");
    const term = document.createElement("dt");
    const description = document.createElement("dd");

    term.textContent = label;
    description.textContent = value || "—";
    wrapper.append(term, description);
    return wrapper;
}

function createPackageCard(packageId, manifest) {
    const card = document.createElement("article");
    const summary = document.createElement("div");
    const id = document.createElement("p");
    const name = document.createElement("h3");
    const description = document.createElement("p");
    const details = document.createElement("dl");

    card.className = "package-card";
    id.className = "package-id";
    id.textContent = packageId;
    name.textContent = manifest.displayName || packageId;
    description.textContent = manifest.description || "No package description provided.";

    summary.append(id, name, description);
    details.append(
        createDefinition("Version", manifest.version),
        createDefinition("Unity", manifest.unity),
        createDefinition("License", manifest.license)
    );
    card.append(summary, details);
    return card;
}

function newestManifest(versions) {
    return Object.values(versions).sort((left, right) =>
        left.version.localeCompare(right.version, undefined, {
            numeric: true,
            sensitivity: "base",
        })
    ).at(-1);
}

async function loadListing() {
    const response = await fetch("./index.json", { cache: "no-store" });
    if (!response.ok) {
        throw new Error(`Repository request failed with HTTP ${response.status}.`);
    }

    const listing = await response.json();
    listingUrl = listing.url;

    document.title = listing.name;
    document.querySelector("#listing-name").textContent = listing.name;
    document.querySelector("#listing-description").textContent = listing.description || "";
    repositoryUrl.textContent = listingUrl;

    const authorName = document.querySelector("#author-name");
    const authorLink = document.querySelector("#author-link");
    authorName.textContent = listing.author;
    if (listing.authorUrl) {
        authorLink.href = listing.authorUrl;
    }

    const infoLink = document.querySelector("#info-link");
    if (listing.infoLink?.url) {
        infoLink.href = listing.infoLink.url;
        infoLink.textContent = listing.infoLink.text || "More information";
        infoLink.hidden = false;
    }

    packageGrid.replaceChildren();
    for (const [packageId, packageEntry] of Object.entries(listing.packages || {})) {
        const manifest = newestManifest(packageEntry.versions || {});
        if (manifest) {
            packageGrid.append(createPackageCard(packageId, manifest));
        }
    }

    if (packageGrid.childElementCount === 0) {
        const emptyMessage = document.createElement("p");
        emptyMessage.className = "loading";
        emptyMessage.textContent = "No published packages are available.";
        packageGrid.append(emptyMessage);
    }

    copyButton.disabled = false;
    addButton.disabled = false;
}

copyButton.addEventListener("click", async () => {
    try {
        await navigator.clipboard.writeText(listingUrl);
        status.textContent = "Repository URL copied.";
    } catch {
        status.textContent = "Copy failed. Select the URL above and copy it manually.";
    }
});

addButton.addEventListener("click", () => {
    window.location.href = `vcc://vpm/addRepo?url=${encodeURIComponent(listingUrl)}`;
});

loadListing().catch((error) => {
    document.querySelector("#listing-name").textContent = "Repository unavailable";
    status.textContent = error.message;
    packageGrid.replaceChildren();
});
