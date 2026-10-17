// Regression for dashboard polling without a browser or external packages.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const assets = path.join(__dirname, "../HardwareMonitorExtension/Assets");
const source = (file) => fs.readFileSync(path.join(assets, file), "utf8");
const html = source("index.html");
const elements = new Map();
const canvas = new Proxy({}, {
  get: (_, name) => name === "createLinearGradient"
    ? () => ({ addColorStop() {} }) : () => {},
});
function element() {
  return {
    textContent: "", innerHTML: "", dataset: {}, clientWidth: 720,
    handlers: {}, classList: { toggle() {} },
    addEventListener(name, callback) { this.handlers[name] = callback; },
    querySelector() { return this.body ??= element(); },
    querySelectorAll() { return []; },
    getContext() { return canvas; },
  };
}
for (const match of html.matchAll(/<[^>]+\bid="([^"]+)"[^>]*>/g)) {
  elements.set(match[1], element());
}
let appendCount = 0;
let removedCount = 0;
let latestScript;
let tick;
let initialReload;
let now = 100000;
class ClockDate extends Date { static now() { return now; } }
const document = {
  title: "", documentElement: { lang: "en" },
  head: { appendChild(script) { appendCount++; latestScript = script; } },
  getElementById(id) {
    assert(elements.has(id), "Unknown DOM id " + id);
    return elements.get(id);
  },
  querySelectorAll() { return []; },
  createElement() {
    return Object.assign(element(), { remove() { removedCount++; } });
  },
};
const stamp1 = "2026-10-17T00:00:00Z";
const stamp2 = "2026-10-17T00:00:02Z";
const reading = { id: "cpu", group: "处理器", name: "CPU 温度",
  short: "CPU", kind: "temperature", unit: "°C",
  value: 42, source: "LHM", history: [41, 42] };
const window = { location: { search: "?lang=en-US" },
  addEventListener() {}, devicePixelRatio: 1,
  __HW_LIVE__: { timestamp: stamp1, sensors: [reading] } };
const context = vm.createContext({
  window, document, navigator: { languages: ["en-US"] }, URLSearchParams,
  Date: ClockDate, console,
  setInterval(callback) { tick = callback; return 1; },
  clearInterval() {},
  setTimeout(callback) { initialReload = callback; },
});
for (const file of ["i18n-data.js", "i18n.js", "app.js"]) {
  vm.runInContext(source(file), context, { filename: file });
}
const t = window.HardwareMonitorI18n.t;
assert.equal(typeof tick, "function", "polling interval exists");
assert(elements.get("metaLine").textContent.includes(t("LiveMode")),
  "packaged live snapshot renders immediately, before any polling");
initialReload();
assert.equal(appendCount, 1, "initial script reload requested");
tick();
assert.equal(appendCount, 1, "in-flight request is not duplicated");
latestScript.onload();
assert.equal(removedCount, 1, "loaded script node removed");
assert(elements.get("metaLine").textContent.includes(t("LiveMode")), "live snapshot shown");
assert(elements.get("dock").innerHTML.includes("data-id=\"cpu\""), "first live sensor defaults to Dock");
elements.get("dataTable").handlers.click({ target: { closest(selector) {
  return selector === "[data-pin]" ? { dataset: { pin: "cpu" } } : null;
} } });
assert.equal(elements.get("dock").innerHTML, "", "deliberately cleared dashboard Dock stays empty");

now += 9000;
tick();
const pending = latestScript;
assert.equal(appendCount, 2, "next poll requested");
tick();
assert.equal(appendCount, 2, "one pending request only");
pending.onload(); // Same timestamp: cached data must remain stale.
const liveTime = new Date(stamp1).toLocaleTimeString("en", { hour12: false });
assert.equal(elements.get("metaLine").textContent,
  t("LiveStopped", liveTime), "unchanged snapshot is stale after 8s");
assert.equal(removedCount, 2, "second script node removed");

window.__HW_LIVE__ = { timestamp: stamp2, sensors: [reading] };
tick();
latestScript.onload();
assert(elements.get("metaLine").textContent.includes(t("LiveMode")),
  "a newer timestamp restores live status");
assert.equal(removedCount, 3, "third script node removed");
assert.equal(elements.get("dock").innerHTML, "", "live refresh does not restore cleared dashboard pins");

tick();
latestScript.onerror();
assert.equal(removedCount, 4, "failed script node removed");
tick();
assert.equal(appendCount, 5, "polling recovers after a failed request");
latestScript.onerror();
assert.equal(removedCount, appendCount, "all script nodes removed");
console.log("PASS dashboard liveness: unchanged timestamp, new timestamp, bounded scripts, retry after load error");
