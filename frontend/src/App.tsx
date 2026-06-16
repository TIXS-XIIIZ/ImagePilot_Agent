import { useEffect, useState } from "react";

type Project = {
  id: number;
  name: string;
  code: string;
  description: string;
  outputFolder: string;
  defaultProviderId?: number;
  fileNamingPattern: string;
};

type Provider = {
  id: number;
  name: string;
  type: string;
  startUrl: string;
  promptInputSelector: string;
  submitButtonSelector: string;
  resultContainerSelector: string;
  downloadButtonSelector: string;
  verificationSelector: string;
  defaultTimeoutSeconds: number;
  delayBetweenJobsSeconds: number;
  manualModeFallback: boolean;
  automationEnabled: boolean;
  browserProfilePath: string;
  browserChannel: string;
};

type ChromeProfile = {
  id: number;
  name: string;
  accountLabel: string;
  startUrl: string;
  browserProfilePath: string;
  browserChannel: string;
  dailyQuota: number;
  usedToday: number;
  status: string;
  notes: string;
};

type Run = {
  id: number;
  projectId: number;
  providerId: number;
  name: string;
  status: string;
  totalJobs: number;
  completedJobs: number;
  failedJobs: number;
  currentJobId?: number;
  createdAt: string;
};

type Job = {
  id: number;
  batchRunId: number;
  projectId: number;
  providerId: number;
  jobNo: number;
  prompt: string;
  status: string;
  outputFilePath?: string;
  errorMessage?: string;
  createdAt: string;
};

type SqlSettings = {
  host: string;
  port: number;
  instanceName: string;
  databaseName: string;
  authenticationMode: string;
  username: string;
  encrypt: boolean;
  trustServerCertificate: boolean;
  passwordSaved: boolean;
  schemaInitialized: boolean;
};

type SqliteSettings = {
  databasePath: string;
  schemaInitialized: boolean;
  lastSyncedAt?: string;
};

type PersistenceSettings = {
  mode: string;
  sqlite: SqliteSettings;
  sqlServer: SqlSettings;
};

type PromptAiSettings = {
  enabled: boolean;
  provider: string;
  baseUrl: string;
  model: string;
  temperature: number;
  apiKeySaved: boolean;
};

type FolderList = {
  currentPath: string;
  parentPath?: string;
  drives: { name: string; path: string }[];
  folders: { name: string; path: string }[];
};

type Snapshot = {
  projects: Project[];
  providers: Provider[];
  chromeProfiles: ChromeProfile[];
  runs: Run[];
  jobs: Job[];
  outputFiles: { id: number; promptJobId: number; filePath: string; fileName: string }[];
};

const apiUrl = "http://localhost:5000/api";
const emptyProject: Project = {
  id: 0,
  name: "",
  code: "",
  description: "",
  outputFolder: "",
  fileNamingPattern: "{projectCode}_{provider}_{yyyyMMdd}_{jobNo}.{ext}",
};
const emptySql: SqlSettings = {
  host: "localhost",
  port: 1433,
  instanceName: "",
  databaseName: "LocalAiImageRunner",
  authenticationMode: "Windows",
  username: "",
  encrypt: true,
  trustServerCertificate: true,
  passwordSaved: false,
  schemaInitialized: false,
};
const emptySqlite: SqliteSettings = {
  databasePath: "",
  schemaInitialized: false,
};
const emptyPromptAi: PromptAiSettings = {
  enabled: false,
  provider: "OpenAiCompatible",
  baseUrl: "https://api.openai.com/v1",
  model: "",
  temperature: 0.7,
  apiKeySaved: false,
};
const emptyChromeProfile: ChromeProfile = {
  id: 0,
  name: "",
  accountLabel: "",
  startUrl: "https://gemini.google.com/app",
  browserProfilePath: "",
  browserChannel: "chrome",
  dailyQuota: 0,
  usedToday: 0,
  status: "Ready",
  notes: "",
};
const promptAiProviders = [
  {
    id: "Gemini",
    title: "Google Gemini",
    baseUrl: "https://generativelanguage.googleapis.com/v1beta",
    models: [
      "gemini-2.5-pro",
      "gemini-2.5-flash",
      "gemini-2.5-flash-lite",
      "gemini-3.1-pro-preview",
      "gemini-3-flash-preview",
      "gemini-3.1-flash-lite",
      "gemini-3.1-flash-lite-preview",
      "gemini-1.5-pro",
    ],
  },
  {
    id: "OpenAiCompatible",
    title: "Custom OpenAI-compatible",
    baseUrl: "https://api.openai.com/v1",
    models: [],
  },
] as const;
const promptPresets = [
  {
    id: "character-design",
    title: "Character Design / ออกแบบตัวละคร",
    description: "ภาพคอนเซ็ปต์เต็มตัว เห็นรายละเอียดชุด สี และบุคลิกชัดเจน",
    prompt: "Create a polished character design sheet.\nCharacter: {{subject}}\nVisual style: {{style}}\nShow a clear full-body front view, readable silhouette, costume details, color palette, facial expression, and production-ready concept art quality.\nBackground: clean neutral background.\nAspect ratio: {{ratio}}",
    negative: "cropped body, extra limbs, duplicate character, unreadable details, cluttered background, text, watermark",
  },
  {
    id: "cartoon",
    title: "Cartoon / ภาพการ์ตูน",
    description: "งานวาดสไตล์การ์ตูน อ่านรูปทรงง่าย สีและเส้นชัด",
    prompt: "Create a charming cartoon illustration.\nSubject: {{subject}}\nCartoon style: {{style}}\nUse clean outlines, expressive features, simple readable shapes, balanced composition, and a cohesive color palette.\nAspect ratio: {{ratio}}",
    negative: "photorealistic rendering, messy outlines, extra limbs, distorted face, text, watermark",
  },
  {
    id: "white-background",
    title: "White Background / พื้นหลังขาวล้วน",
    description: "พื้นหลังสีขาว ไม่มีแสง ไม่มีเงา ไม่มี reflection หรือ gradient",
    prompt: "Create a clean isolated image of {{subject}}.\nStyle: {{style}}\nBackground: pure solid white (#FFFFFF).\nLighting constraint: flat even color only, no directional light, no dramatic lighting, no cast shadow, no contact shadow, no reflection, no gradient, no ambient shading.\nComposition: centered object with generous clear space around it.\nAspect ratio: {{ratio}}",
    negative: "shadow, cast shadow, contact shadow, reflection, gradient, spotlight, vignette, textured background, floor line, text, watermark",
  },
  {
    id: "product-cutout",
    title: "Product Cutout / รูปสินค้า",
    description: "รูปสินค้าแบบ catalog เห็นวัตถุครบ ขอบคม พร้อมใช้งานขายของ",
    prompt: "Create a professional e-commerce product cutout of {{subject}}.\nStyle: {{style}}\nComposition: centered product, complete object visible, crisp edges, catalog-ready presentation.\nBackground: pure white seamless background.\nAspect ratio: {{ratio}}",
    negative: "cropped product, busy scene, props, text, watermark, blurry edges, distorted packaging",
  },
  {
    id: "food-poster",
    title: "Food Poster / โปสเตอร์อาหาร",
    description: "ภาพอาหารสำหรับโปรโมชันหรือเมนู ดูน่ากินและจัดองค์ประกอบชัด",
    prompt: "Create an appetizing food promotional poster.\nFood subject: {{subject}}\nVisual style: {{style}}\nComposition: clear hero food item, organized menu-poster layout, inviting colors, clean space for optional copy, commercial food photography quality.\nAspect ratio: {{ratio}}",
    negative: "unappetizing food, cluttered layout, illegible text, distorted ingredients, watermark",
  },
] as const;

async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiUrl}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      "X-ImagePilot-Token": "imagepilot-local-ui",
      ...init?.headers,
    },
  });
  if (!response.ok) {
    const body = await response.json().catch(() => ({ message: response.statusText }));
    throw new Error(body.message ?? response.statusText);
  }
  return response.status === 204 ? (undefined as T) : response.json();
}

export function App() {
  const [tab, setTab] = useState("dashboard");
  const [snapshot, setSnapshot] = useState<Snapshot>({ projects: [], providers: [], chromeProfiles: [], runs: [], jobs: [], outputFiles: [] });
  const [selectedRunId, setSelectedRunId] = useState<number>();
  const [notice, setNotice] = useState("Ready");
  const [errorMessage, setErrorMessage] = useState("");
  const [projectDraft, setProjectDraft] = useState<Project>(emptyProject);
  const [providerDraft, setProviderDraft] = useState<Provider>();
  const [sql, setSql] = useState<SqlSettings>(emptySql);
  const [sqlite, setSqlite] = useState<SqliteSettings>(emptySqlite);
  const [persistenceMode, setPersistenceMode] = useState("Json");
  const [sqlPassword, setSqlPassword] = useState("");
  const [promptAi, setPromptAi] = useState<PromptAiSettings>(emptyPromptAi);
  const [promptAiApiKey, setPromptAiApiKey] = useState("");
  const [chromeProfileDraft, setChromeProfileDraft] = useState<ChromeProfile>(emptyChromeProfile);
  const [profileProviderId, setProfileProviderId] = useState<number>(0);
  const [isEnhancingPrompt, setIsEnhancingPrompt] = useState(false);
  const [selectedPresetId, setSelectedPresetId] = useState("character-design");
  const [folderList, setFolderList] = useState<FolderList>();
  const [newFolderName, setNewFolderName] = useState("");
  const [folderMessage, setFolderMessage] = useState("");
  const [builder, setBuilder] = useState({
    projectId: 0,
    providerId: 0,
    basePrompt: "Create a polished product image.\nSubject: {{subject}}\nStyle: {{style}}\nAspect ratio: {{ratio}}",
    negativePrompt: "",
    variablesJson: '{\n  "subject": ["mini sandwich", "iced coffee"],\n  "style": ["clean studio", "warm cafe"],\n  "ratio": ["1:1", "9:16"]\n}',
    count: 4,
  });

  const refresh = async () => {
    try {
      const data = await api<Snapshot>("/dashboard");
      setSnapshot(data);
      setSelectedRunId((current) => current ?? data.runs.at(-1)?.id);
      setBuilder((current) => ({
        ...current,
        projectId: current.projectId || data.projects[0]?.id || 0,
        providerId: current.providerId || data.providers[0]?.id || 0,
      }));
      setProfileProviderId((current) => current || data.providers.find((item) => item.name.includes("Gemini"))?.id || data.providers[0]?.id || 0);
    } catch (error) {
      const message = error instanceof Error ? error.message : "Cannot reach the API";
      setErrorMessage(message);
      setNotice(message);
    }
  };

  useEffect(() => {
    refresh();
    api<PromptAiSettings>("/settings/prompt-ai").then(setPromptAi).catch(showError);
    const interval = window.setInterval(refresh, 1500);
    return () => window.clearInterval(interval);
  }, []);

  useEffect(() => {
    if (tab === "sql") {
      loadPersistence().catch(showError);
    }
  }, [tab]);

  useEffect(() => {
    if (tab === "prompt-ai") {
      api<PromptAiSettings>("/settings/prompt-ai").then(setPromptAi).catch(showError);
    }
  }, [tab]);

  const currentRun = snapshot.runs.find((run) => run.id === selectedRunId) ?? snapshot.runs.at(-1);
  const runJobs = snapshot.jobs.filter((job) => job.batchRunId === currentRun?.id);
  const currentJob = runJobs.find((job) => job.id === currentRun?.currentJobId)
    ?? runJobs.find((job) => job.status === "WaitingForUser")
    ?? runJobs.find((job) => job.status === "Pending");
  const project = snapshot.projects.find((item) => item.id === currentRun?.projectId);
  const provider = snapshot.providers.find((item) => item.id === currentRun?.providerId);
  const promptAiReady = promptAi.enabled && promptAi.apiKeySaved;
  const promptAiStatus = promptAiReady
    ? `พร้อมใช้งาน: ${promptAi.provider}${promptAi.model ? ` / ${promptAi.model}` : ""}`
    : promptAi.enabled
      ? "ยังไม่มี API key ที่บันทึกไว้ ให้ไปหน้า Prompt AI API แล้ว Save API settings ก่อน"
      : "ยังไม่ได้เปิดใช้งาน ให้ไปหน้า Prompt AI API แล้วติ๊ก Enable external AI prompt improvement";

  function showNotice(message: string) {
    setErrorMessage("");
    setNotice(message);
  }

  function showError(error: unknown) {
    const message = error instanceof Error ? error.message : "Unexpected error";
    setErrorMessage(message);
    setNotice(message);
  }

  async function loadPersistence() {
    const settings = await api<PersistenceSettings>("/settings/persistence");
    setPersistenceMode(settings.mode || "Json");
    setSqlite(settings.sqlite);
    setSql(settings.sqlServer);
  }

  async function copyPromptToClipboard(prompt?: string, announce = true) {
    if (!prompt) {
      return false;
    }

    try {
      await navigator.clipboard.writeText(prompt);
      if (announce) {
        showNotice("คัดลอก prompt แล้ว");
      }
      return true;
    } catch {
      const message = "Browser ไม่อนุญาตให้คัดลอก clipboard อัตโนมัติ ให้กดปุ่ม “คัดลอก prompt” โดยตรง หรือคลิกในช่อง prompt แล้วกด Ctrl+A จากนั้น Ctrl+C";
      if (announce) {
        setErrorMessage(message);
        setNotice(message);
      }
      return false;
    }
  }

  async function runAction(action: string) {
    if (!currentRun) return;
    try {
      await api(`/batch-runs/${currentRun.id}/${action}`, { method: "POST" });
      showNotice(`${action} sent for ${currentRun.name}`);
      await refresh();
    } catch (error) {
      showError(error);
    }
  }

  async function startRun() {
    if (!currentRun) return;
    if (!provider || provider.automationEnabled) {
      await runAction("start");
      return;
    }

    try {
      await api(`/batch-runs/${currentRun.id}/start`, { method: "POST" });
      const data = await api<Snapshot>("/dashboard");
      setSnapshot(data);
      setSelectedRunId(currentRun.id);
      const updatedRun = data.runs.find((run) => run.id === currentRun.id);
      const nextJob = data.jobs.find((job) => job.id === updatedRun?.currentJobId)
        ?? data.jobs.find((job) => job.batchRunId === currentRun.id && job.status === "WaitingForUser")
        ?? data.jobs.find((job) => job.batchRunId === currentRun.id && job.status === "Pending");

      const copied = nextJob ? await copyPromptToClipboard(nextJob.prompt, false) : false;
      await api<{ message: string }>(`/providers/${provider.id}/open-system`, { method: "POST" });
      if (!nextJob) {
        showNotice("Manual mode started. Open your logged-in Chrome tab and continue from the current job.");
        return;
      }

      if (copied) {
        showNotice(`Manual mode started. Job #${nextJob.jobNo} prompt copied. Paste it in your logged-in Chrome tab, generate the image, then mark complete.`);
        return;
      }

      const message = "เปิด Chrome ให้แล้ว แต่ browser ไม่อนุญาตให้คัดลอก clipboard อัตโนมัติ ให้กด “คัดลอก prompt” หรือคัดลอกจากช่อง prompt เอง";
      setErrorMessage(message);
      setNotice(message);
    } catch (error) {
      showError(error);
    }
  }

  async function saveProject() {
    try {
      await api(`/projects${projectDraft.id ? `/${projectDraft.id}` : ""}`, {
        method: projectDraft.id ? "PUT" : "POST",
        body: JSON.stringify(projectDraft),
      });
      setProjectDraft(emptyProject);
      showNotice("Project saved");
      await refresh();
    } catch (error) {
      showError(error);
    }
  }

  async function browseOutputFolder() {
    try {
      showNotice("Loading folders...");
      const result = await api<FolderList>("/local-folders/list", {
        method: "POST",
        body: JSON.stringify({ currentPath: projectDraft.outputFolder }),
      });
      setFolderList(result);
      setFolderMessage("");
      showNotice("Choose an output folder");
    } catch (error) {
      showError(error);
    }
  }

  async function openFolder(path?: string) {
    if (!path) return;
    try {
      setFolderList(await api<FolderList>("/local-folders/list", {
        method: "POST",
        body: JSON.stringify({ currentPath: path }),
      }));
      setFolderMessage("");
    } catch (error) {
      showError(error);
    }
  }

  function useCurrentFolder() {
    if (!folderList) return;
    setProjectDraft((current) => ({ ...current, outputFolder: folderList.currentPath }));
    setFolderList(undefined);
    setFolderMessage("");
    showNotice("Output folder selected");
  }

  async function createFolder() {
    if (!folderList) return;
    try {
      const result = await api<FolderList>("/local-folders/create", {
        method: "POST",
        body: JSON.stringify({ currentPath: folderList.currentPath, folderName: newFolderName }),
      });
      setFolderList(result);
      setNewFolderName("");
      setFolderMessage("Folder created. This new folder is now selected.");
      showNotice("New folder created");
    } catch (error) {
      setFolderMessage(error instanceof Error ? error.message : "Cannot create folder");
      showError(error);
    }
  }

  async function createBatch() {
    try {
      const run = await api<Run>("/batch-runs", { method: "POST", body: JSON.stringify(builder) });
      setSelectedRunId(run.id);
      showNotice(`${run.name} created with ${builder.count} jobs`);
      setTab("dashboard");
      await refresh();
    } catch (error) {
      showError(error);
    }
  }

  async function saveProvider() {
    if (!providerDraft) return;
    try {
      await api(`/providers/${providerDraft.id}`, { method: "PUT", body: JSON.stringify(providerDraft) });
      showNotice(`${providerDraft.name} saved`);
      await refresh();
    } catch (error) {
      showError(error);
    }
  }

  async function setProviderAutomation(enabled: boolean) {
    if (!provider) return;
    try {
      await api(`/providers/${provider.id}`, {
        method: "PUT",
        body: JSON.stringify({ ...provider, automationEnabled: enabled }),
      });
      showNotice(enabled
        ? `${provider.name} automation enabled. Log in once in the ImagePilot profile, then press Start.`
        : `${provider.name} automation disabled. Manual mode is active.`);
      await refresh();
    } catch (error) {
      showError(error);
    }
  }

  async function saveChromeProfile() {
    try {
      const saved = await api<ChromeProfile>(`/chrome-profiles${chromeProfileDraft.id ? `/${chromeProfileDraft.id}` : ""}`, {
        method: chromeProfileDraft.id ? "PUT" : "POST",
        body: JSON.stringify(chromeProfileDraft),
      });
      setChromeProfileDraft(saved);
      showNotice(`${saved.name} saved`);
      await refresh();
    } catch (error) {
      showError(error);
    }
  }

  async function openChromeProfile(id: number) {
    try {
      const response = await api<{ message: string }>(`/chrome-profiles/${id}/open`, { method: "POST" });
      showNotice(response.message);
    } catch (error) {
      showError(error);
    }
  }

  async function assignChromeProfile(profileId: number, providerId = profileProviderId) {
    if (!providerId) {
      showError(new Error("Choose a provider before assigning this Chrome profile."));
      return;
    }

    try {
      await api(`/chrome-profiles/${profileId}/assign`, {
        method: "POST",
        body: JSON.stringify({ providerId }),
      });
      const assignedProvider = snapshot.providers.find((item) => item.id === providerId);
      showNotice(`Profile assigned to ${assignedProvider?.name ?? "provider"} and automation enabled`);
      await refresh();
    } catch (error) {
      showError(error);
    }
  }

  async function openProvider(id: number) {
    try {
      const response = await api<{ message: string }>(`/providers/${id}/open`, { method: "POST" });
      showNotice(response.message);
    } catch (error) {
      showError(error);
    }
  }

  async function openSystemProvider(id: number) {
    try {
      const response = await api<{ message: string }>(`/providers/${id}/open-system`, { method: "POST" });
      showNotice(response.message);
    } catch (error) {
      showError(error);
    }
  }

  async function completeJob() {
    if (!currentRun || !currentJob) return;
    try {
      await api(`/batch-runs/${currentRun.id}/jobs/${currentJob.id}/complete`, {
        method: "POST",
        body: JSON.stringify({ outputFilePath: null }),
      });
      showNotice(`Job ${currentJob.jobNo} completed`);
      await refresh();
    } catch (error) {
      showError(error);
    }
  }

  async function testSql() {
    try {
      const result = await api<{ success: boolean; message: string }>("/settings/sql-server/test", {
        method: "POST",
        body: JSON.stringify({ settings: sql, password: sqlPassword }),
      });
      showNotice(result.message);
    } catch (error) {
      showError(error);
    }
  }

  async function saveSql() {
    try {
      await api("/settings/sql-server/save", { method: "POST", body: JSON.stringify({ settings: sql, password: sqlPassword }) });
      setSqlPassword("");
      await loadPersistence();
      showNotice("SQL Server settings saved");
    } catch (error) {
      showError(error);
    }
  }

  async function initializeSql() {
    try {
      const result = await api<{ success: boolean; message: string }>("/settings/sql-server/initialize", { method: "POST" });
      showNotice(result.message);
      await loadPersistence();
    } catch (error) {
      showError(error);
    }
  }

  async function savePersistenceMode(mode = persistenceMode) {
    try {
      const result = await api<{ success: boolean; message: string }>("/settings/persistence/mode", {
        method: "POST",
        body: JSON.stringify({ mode }),
      });
      setPersistenceMode(mode);
      showNotice(result.message);
    } catch (error) {
      showError(error);
    }
  }

  async function testSqlite() {
    try {
      const result = await api<{ success: boolean; message: string }>("/settings/sqlite/test", {
        method: "POST",
        body: JSON.stringify({ settings: sqlite }),
      });
      showNotice(result.message);
    } catch (error) {
      showError(error);
    }
  }

  async function saveSqlite() {
    try {
      const result = await api<{ success: boolean; message: string }>("/settings/sqlite/save", {
        method: "POST",
        body: JSON.stringify({ settings: sqlite }),
      });
      await loadPersistence();
      showNotice(result.message);
    } catch (error) {
      showError(error);
    }
  }

  async function initializeSqlite() {
    try {
      const result = await api<{ success: boolean; message: string }>("/settings/sqlite/initialize", { method: "POST" });
      await loadPersistence();
      showNotice(result.message);
    } catch (error) {
      showError(error);
    }
  }

  function applyPreset(presetId: string) {
    const preset = promptPresets.find((item) => item.id === presetId);
    if (!preset) return;
    setSelectedPresetId(preset.id);
    setBuilder((current) => ({ ...current, basePrompt: preset.prompt, negativePrompt: preset.negative }));
    showNotice(`${preset.title} preset applied`);
  }

  async function enhancePrompt() {
    if (!promptAi.enabled) {
      showError(new Error("ยังไม่ได้เปิดใช้งาน Prompt AI API: ไปที่หน้า Prompt AI API แล้วติ๊ก Enable external AI prompt improvement ก่อนครับ"));
      return;
    }
    if (!promptAi.apiKeySaved) {
      showError(new Error("ยังไม่มี API key ที่บันทึกไว้: ไปที่หน้า Prompt AI API ใส่ key แล้วกด Save API settings ก่อนครับ"));
      return;
    }
    try {
      setIsEnhancingPrompt(true);
      showNotice("กำลังให้ AI ช่วยปรับ prompt...");
      const preset = promptPresets.find((item) => item.id === selectedPresetId);
      const result = await api<{ success: boolean; message: string; prompt?: string }>("/prompts/enhance", {
        method: "POST",
        body: JSON.stringify({
          prompt: builder.basePrompt,
          category: preset?.title ?? "General",
          extraInstructions: builder.negativePrompt ? `Avoid: ${builder.negativePrompt}` : "",
        }),
      });
      if (!result.success || !result.prompt) {
        throw new Error(result.message);
      }
      setBuilder((current) => ({ ...current, basePrompt: result.prompt ?? current.basePrompt }));
      showNotice(result.message || "AI ช่วยปรับ prompt ให้แล้ว");
    } catch (error) {
      showError(error);
    } finally {
      setIsEnhancingPrompt(false);
    }
  }

  async function testPromptAi() {
    try {
      const result = await api<{ success: boolean; message: string }>("/settings/prompt-ai/test", {
        method: "POST",
        body: JSON.stringify({ settings: promptAi, apiKey: promptAiApiKey }),
      });
      showNotice(result.message);
    } catch (error) {
      showError(error);
    }
  }

  async function savePromptAi() {
    try {
      await api("/settings/prompt-ai/save", {
        method: "POST",
        body: JSON.stringify({ settings: promptAi, apiKey: promptAiApiKey }),
      });
      setPromptAiApiKey("");
      setPromptAi(await api<PromptAiSettings>("/settings/prompt-ai"));
      showNotice("External prompt API settings saved");
    } catch (error) {
      showError(error);
    }
  }

  function changePromptAiProvider(providerId: string) {
    const provider = promptAiProviders.find((item) => item.id === providerId);
    if (!provider) return;
    setPromptAi((current) => ({
      ...current,
      provider: provider.id,
      baseUrl: provider.baseUrl,
      model: provider.models[0] ?? "",
    }));
  }

  const stats = {
    total: currentRun?.totalJobs ?? 0,
    completed: currentRun?.completedJobs ?? 0,
    failed: currentRun?.failedJobs ?? 0,
    waiting: runJobs.filter((job) => job.status === "WaitingForUser").length,
  };

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <div className="brand-mark">IP</div>
          <div><strong>ImagePilot</strong><span>Local batch agent</span></div>
        </div>
        <nav>
          {[
            ["dashboard", "Dashboard"],
            ["builder", "Prompt Builder"],
            ["projects", "Projects"],
            ["providers", "Providers"],
            ["profiles", "Chrome Profiles"],
            ["history", "History"],
            ["prompt-ai", "Prompt AI API"],
            ["sql", "Database"],
          ].map(([id, label]) => (
            <button key={id} className={tab === id ? "nav-active" : ""} onClick={() => setTab(id)}>{label}</button>
          ))}
        </nav>
        <div className="sidebar-note">Local-only workspace<br />Browser profiles stay on this machine.</div>
      </aside>

      <main>
        <header className="topbar">
          <div>
            <p className="eyebrow">LOCAL CREATIVE OPERATIONS</p>
            <h1>{tab === "dashboard" ? "Batch control room" : tab.replace("-", " ")}</h1>
          </div>
          <div className="status-pill"><span />{notice}</div>
        </header>
        {errorMessage && (
          <div className="error-banner" role="alert">
            <div>
              <strong>เกิดข้อผิดพลาด</strong>
              <p>{errorMessage}</p>
            </div>
            <button type="button" onClick={() => setErrorMessage("")}>ปิด</button>
          </div>
        )}

        {tab === "dashboard" && (
          <>
            <section className="hero-card">
              <div>
                <p className="eyebrow">ACTIVE RUN</p>
                <h2>{currentRun?.name ?? "No batch selected"}</h2>
                <p>{project?.name ?? "Create a project and prepare your first prompt batch."}</p>
              </div>
              <div className={`run-state state-${(currentRun?.status ?? "idle").toLowerCase()}`}>{currentRun?.status ?? "Idle"}</div>
            </section>
            <section className="stats-grid">
              <Stat label="Total jobs" value={stats.total} />
              <Stat label="Completed" value={stats.completed} tone="green" />
              <Stat label="Failed" value={stats.failed} tone="red" />
              <Stat label="Manual action" value={stats.waiting} tone="amber" />
            </section>
            <section className="dashboard-grid">
              <article className="panel current-panel">
                <div className="panel-title"><div><p className="eyebrow">CURRENT JOB</p><h3>Prompt dispatch</h3></div><span>#{currentJob?.jobNo ?? "--"}</span></div>
                <textarea readOnly value={currentJob?.prompt ?? "Your current prompt will appear here."} />
                {currentJob?.errorMessage && <p className="warning">{currentJob.errorMessage}</p>}
                <div className="button-row">
                  <button className="primary" disabled={!currentJob} onClick={() => copyPromptToClipboard(currentJob?.prompt)}>คัดลอก prompt</button>
                  <button disabled={!currentJob} onClick={completeJob}>ทำเสร็จแล้ว</button>
                  <button disabled={!provider} onClick={() => provider && openSystemProvider(provider.id)}>เปิด Chrome ที่ใช้อยู่</button>
                  <button disabled={!provider} onClick={() => provider && openProvider(provider.id)}>เปิด profile แยก</button>
                </div>
              </article>
              <article className="panel">
                <div className="panel-title"><div><p className="eyebrow">RUN CONTROL</p><h3>{provider?.name ?? "Provider"}</h3></div></div>
                {provider && (
                  <div className={`automation-card ${provider.automationEnabled ? "automation-on" : "automation-off"}`}>
                    <Field label="เลือกระบบ Browser ที่จะใช้งาน">
                      <select 
                        value={provider.automationEnabled && provider.browserProfilePath ? provider.browserProfilePath : "manual"} 
                        onChange={(e) => {
                          if (e.target.value === "manual") {
                             setProviderAutomation(false);
                          } else {
                             const profile = snapshot.chromeProfiles.find(p => p.browserProfilePath === e.target.value);
                             if (profile) assignChromeProfile(profile.id, provider.id);
                          }
                        }}
                      >
                        <option value="manual">ใช้ Chrome ปัจจุบันของคุณ (แบบ Manual)</option>
                        <optgroup label="ใช้โปรไฟล์ ImagePilot (แบบ Auto)">
                          {snapshot.chromeProfiles.map(p => (
                            <option key={p.id} value={p.browserProfilePath}>{p.name} {p.accountLabel ? `(${p.accountLabel})` : ""}</option>
                          ))}
                        </optgroup>
                      </select>
                    </Field>
                    <p className="muted" style={{ marginTop: 8, fontSize: '0.85em' }}>
                      {provider.automationEnabled 
                        ? "ระบบจะควบคุมและพิมพ์ให้อัตโนมัติใน Profile ที่คุณเลือก (กดปุ่มด้านล่างเพื่อเปิดหน้าต่าง Login ทิ้งไว้ก่อนรัน)" 
                        : "แบบ Manual: ระบบจะไม่ควบคุม browser ตอนกด Start คุณจะต้องกดคัดลอก prompt ไปวางใน Chrome ที่ใช้อยู่เอง"}
                    </p>
                    {provider.automationEnabled && (
                      <div className="button-row compact-row" style={{ marginTop: 12 }}>
                        <button type="button" onClick={() => openProvider(provider.id)}>เปิด Chrome Profile ที่เลือกไว้</button>
                      </div>
                    )}
                  </div>
                )}
                <div className="manual-browser-note">
                  <strong>ใช้ browser ที่คุณ login ไว้แล้ว</strong>
                  <p>โหมด Manual จะไม่เปิด browser ใหม่อัตโนมัติ ให้กด “คัดลอก prompt” แล้วนำไปวางใน Gemini / AI Studio ที่คุณเปิดและ login ไว้ จากนั้นกลับมากด “ทำเสร็จแล้ว”</p>
                  <p>ปุ่ม “เปิด Chrome ที่ใช้อยู่” จะเปิดเว็บผ่าน browser ปกติของ Windows ถ้า Chrome เป็น browser หลักก็จะใช้ session/login เดิมของคุณ</p>
                  <p>ปุ่ม “เปิด profile แยก” จะเปิด browser profile ของ ImagePilot ซึ่งเป็น session แยก จึงอาจต้อง login ใหม่</p>
                </div>
                <div className="control-grid">
                  <button className="primary" onClick={startRun} disabled={!currentRun}>Start</button>
                  <button onClick={() => runAction("pause")} disabled={!currentRun}>Pause</button>
                  <button onClick={() => runAction("resume")} disabled={!currentRun}>Resume</button>
                  <button className="danger" onClick={() => runAction("stop")} disabled={!currentRun}>Stop</button>
                </div>
                <select value={currentRun?.id ?? ""} onChange={(event) => setSelectedRunId(Number(event.target.value))}>
                  <option value="">Select a batch run</option>
                  {snapshot.runs.map((run) => <option key={run.id} value={run.id}>{run.name} - {run.status}</option>)}
                </select>
              </article>
            </section>
          </>
        )}

        {tab === "builder" && (
          <section className="panel form-panel">
            <div className="panel-title"><div><p className="eyebrow">PROMPT LAB</p><h2>สร้างชุด Prompt สำหรับรูปภาพ</h2></div></div>
            <div className="builder-help">
              <div>
                <strong>วิธีใช้หน้านี้</strong>
                <p>เลือกหมวดภาพก่อน แล้วแก้ข้อความในช่อง Prompt ตามที่ต้องการ ถ้าต้องการให้ AI ที่คุณใส่ API key ไว้ช่วยคิด ให้กดปุ่ม “ให้ AI ช่วยคิด Prompt”</p>
              </div>
              <span className={promptAiReady ? "ai-ready" : "ai-warning"}>{promptAiStatus}</span>
            </div>
            <p className="muted">กดเลือก preset เพื่อใส่โครง prompt อัตโนมัติ จากนั้นปรับ subject, style, ratio หรือรายละเอียดอื่น ๆ ได้เอง</p>
            <div className="preset-grid">
              {promptPresets.map((preset) => (
                <button key={preset.id} className={`preset-card ${selectedPresetId === preset.id ? "preset-active" : ""}`} onClick={() => applyPreset(preset.id)}>
                  <strong>{preset.title}</strong>
                  <span>{preset.description}</span>
                </button>
              ))}
            </div>
            <div className="two-cols">
              <Field label="โปรเจค (Project)"><select value={builder.projectId} onChange={(event) => setBuilder({ ...builder, projectId: Number(event.target.value) })}>{snapshot.projects.map((item) => <option value={item.id} key={item.id}>{item.name}</option>)}</select></Field>
              <Field label="เว็บ/ผู้ให้บริการสร้างรูป (Provider)"><select value={builder.providerId} onChange={(event) => setBuilder({ ...builder, providerId: Number(event.target.value) })}>{snapshot.providers.map((item) => <option value={item.id} key={item.id}>{item.name}</option>)}</select></Field>
            </div>
            <Field label="Prompt หลัก (Base prompt)"><textarea rows={8} value={builder.basePrompt} onChange={(event) => setBuilder({ ...builder, basePrompt: event.target.value })} /></Field>
            <p className="field-help">ช่องนี้คือ prompt ที่จะส่งให้ Gemini / ผู้ให้บริการสร้างรูป คุณสามารถกดให้ AI ช่วยขยายให้ละเอียดขึ้นได้</p>
            <Field label="สิ่งที่ไม่ต้องการ (Negative prompt)"><input value={builder.negativePrompt} onChange={(event) => setBuilder({ ...builder, negativePrompt: event.target.value })} /></Field>
            <Field label="ตัวแปรสำหรับสร้างหลาย Prompt (Variables JSON)"><textarea rows={7} value={builder.variablesJson} onChange={(event) => setBuilder({ ...builder, variablesJson: event.target.value })} /></Field>
            <p className="field-help">ตัวอย่าง: ใส่ subject หลายแบบและ ratio หลายแบบ โปรแกรมจะผสมออกมาเป็นหลาย job ตามจำนวนด้านล่าง</p>
            <Field label="จำนวนงานที่จะสร้าง (Number of jobs)"><input type="number" min="1" max="200" value={builder.count} onChange={(event) => setBuilder({ ...builder, count: Number(event.target.value) })} /></Field>
            <div className="button-row">
              <button onClick={enhancePrompt} disabled={isEnhancingPrompt}>{isEnhancingPrompt ? "AI กำลังช่วยคิด..." : "ให้ AI ช่วยคิด Prompt"}</button>
              <button className="primary" onClick={createBatch}>สร้างรายการ Prompt</button>
            </div>
          </section>
        )}

        {tab === "projects" && (
          <section className="split-grid">
            <article className="panel list-panel"><h3>Projects</h3>{snapshot.projects.map((item) => <button key={item.id} onClick={() => setProjectDraft(item)}><strong>{item.name}</strong><span>{item.outputFolder}</span></button>)}</article>
            <article className="panel form-panel">
              <h3>{projectDraft.id ? "Edit project" : "New project"}</h3>
              <Field label="Project name"><input value={projectDraft.name} onChange={(event) => setProjectDraft({ ...projectDraft, name: event.target.value })} /></Field>
              <Field label="Project code"><input value={projectDraft.code} onChange={(event) => setProjectDraft({ ...projectDraft, code: event.target.value })} /></Field>
              <Field label="Description"><textarea value={projectDraft.description} onChange={(event) => setProjectDraft({ ...projectDraft, description: event.target.value })} /></Field>
              <Field label="Output folder"><div className="input-action-row"><input placeholder="Leave empty for the local projects folder" value={projectDraft.outputFolder} onChange={(event) => setProjectDraft({ ...projectDraft, outputFolder: event.target.value })} /><button type="button" onClick={browseOutputFolder}>Browse...</button></div></Field>
              <Field label="Naming pattern"><input value={projectDraft.fileNamingPattern} onChange={(event) => setProjectDraft({ ...projectDraft, fileNamingPattern: event.target.value })} /></Field>
              <div className="button-row"><button className="primary" onClick={saveProject}>Save project</button><button onClick={() => setProjectDraft(emptyProject)}>Clear</button></div>
            </article>
          </section>
        )}

        {tab === "profiles" && (
          <section className="split-grid">
            <article className="panel list-panel">
              <h3>Chrome Profiles</h3>
              {snapshot.chromeProfiles.length === 0 && <p className="muted">Create one profile per Google ID, then open it and log in once.</p>}
              {snapshot.chromeProfiles.map((item) => (
                <button key={item.id} onClick={() => setChromeProfileDraft(item)}>
                  <strong>{item.name}</strong>
                  <span>{item.accountLabel || "No account label"} · {item.status}</span>
                  <span>{item.browserProfilePath}</span>
                </button>
              ))}
            </article>
            <article className="panel form-panel">
              <div className="panel-title">
                <div><p className="eyebrow">AUTOMATION PROFILES</p><h2>{chromeProfileDraft.id ? "Edit Chrome profile" : "New Chrome profile"}</h2></div>
              </div>
              <p className="muted">Use one profile for each Google account. Open the profile, log in once, then assign it to Gemini Web for automation.</p>
              <div className="two-cols">
                <Field label="Profile name"><input placeholder="Gemini ID 1" value={chromeProfileDraft.name} onChange={(event) => setChromeProfileDraft({ ...chromeProfileDraft, name: event.target.value })} /></Field>
                <Field label="Account label"><input placeholder="email or nickname" value={chromeProfileDraft.accountLabel} onChange={(event) => setChromeProfileDraft({ ...chromeProfileDraft, accountLabel: event.target.value })} /></Field>
              </div>
              <Field label="Start URL"><input value={chromeProfileDraft.startUrl} onChange={(event) => setChromeProfileDraft({ ...chromeProfileDraft, startUrl: event.target.value })} /></Field>
              <Field label="Profile folder"><input placeholder="Leave empty to auto-create under browser-profiles" value={chromeProfileDraft.browserProfilePath} onChange={(event) => setChromeProfileDraft({ ...chromeProfileDraft, browserProfilePath: event.target.value })} /></Field>
              <Field label="Browser channel"><select value={chromeProfileDraft.browserChannel} onChange={(event) => setChromeProfileDraft({ ...chromeProfileDraft, browserChannel: event.target.value })}>
                <option value="chrome">Installed Google Chrome (recommended for Google login)</option>
                <option value="">Bundled Chromium</option>
                <option value="msedge">Microsoft Edge</option>
              </select></Field>
              <div className="two-cols">
                <Field label="Daily quota"><input type="number" min="0" value={chromeProfileDraft.dailyQuota} onChange={(event) => setChromeProfileDraft({ ...chromeProfileDraft, dailyQuota: Number(event.target.value) })} /></Field>
                <Field label="Used today"><input type="number" min="0" value={chromeProfileDraft.usedToday} onChange={(event) => setChromeProfileDraft({ ...chromeProfileDraft, usedToday: Number(event.target.value) })} /></Field>
              </div>
              <Field label="Status"><select value={chromeProfileDraft.status} onChange={(event) => setChromeProfileDraft({ ...chromeProfileDraft, status: event.target.value })}>
                <option value="Ready">Ready</option>
                <option value="Assigned">Assigned</option>
                <option value="QuotaReached">Quota reached</option>
                <option value="NeedsLogin">Needs login</option>
                <option value="Blocked">Blocked</option>
              </select></Field>
              <Field label="Notes"><textarea rows={3} value={chromeProfileDraft.notes} onChange={(event) => setChromeProfileDraft({ ...chromeProfileDraft, notes: event.target.value })} /></Field>
              <div className="button-row">
                <button className="primary" onClick={saveChromeProfile}>Save profile</button>
                <button onClick={() => setChromeProfileDraft(emptyChromeProfile)}>New profile</button>
                <button disabled={!chromeProfileDraft.id} onClick={() => openChromeProfile(chromeProfileDraft.id)}>Open login profile</button>
              </div>
              <div className="assign-panel">
                <strong>Assign this profile to automation</strong>
                <p>Assigning sets the selected provider to use this Chrome profile folder and turns automation on.</p>
                <div className="input-action-row">
                  <select value={profileProviderId} onChange={(event) => setProfileProviderId(Number(event.target.value))}>
                    {snapshot.providers.map((item) => <option value={item.id} key={item.id}>{item.name}</option>)}
                  </select>
                  <button disabled={!chromeProfileDraft.id} onClick={() => assignChromeProfile(chromeProfileDraft.id)}>Assign</button>
                </div>
              </div>
            </article>
          </section>
        )}

        {tab === "providers" && (
          <section className="split-grid">
            <article className="panel list-panel"><h3>Providers</h3>{snapshot.providers.map((item) => <button key={item.id} onClick={() => setProviderDraft(item)}><strong>{item.name}</strong><span>{item.automationEnabled ? "Automation enabled" : "Manual fallback"}</span></button>)}</article>
            <article className="panel form-panel">
              <h3>{providerDraft?.name ?? "Choose a provider"}</h3>
              {providerDraft && <>
                <Field label="Start URL"><input value={providerDraft.startUrl} onChange={(event) => setProviderDraft({ ...providerDraft, startUrl: event.target.value })} /></Field>
                <Field label="Prompt selector"><input value={providerDraft.promptInputSelector} onChange={(event) => setProviderDraft({ ...providerDraft, promptInputSelector: event.target.value })} /></Field>
                <Field label="Submit selector"><input value={providerDraft.submitButtonSelector} onChange={(event) => setProviderDraft({ ...providerDraft, submitButtonSelector: event.target.value })} /></Field>
                <Field label="Result selector"><input value={providerDraft.resultContainerSelector} onChange={(event) => setProviderDraft({ ...providerDraft, resultContainerSelector: event.target.value })} /></Field>
                <Field label="Download selector"><input value={providerDraft.downloadButtonSelector} onChange={(event) => setProviderDraft({ ...providerDraft, downloadButtonSelector: event.target.value })} /></Field>
                <label className="check"><input type="checkbox" checked={providerDraft.automationEnabled} onChange={(event) => setProviderDraft({ ...providerDraft, automationEnabled: event.target.checked })} /> Enable selector automation</label>
                <label className="check"><input type="checkbox" checked={providerDraft.manualModeFallback} onChange={(event) => setProviderDraft({ ...providerDraft, manualModeFallback: event.target.checked })} /> Pause for manual fallback when automation fails</label>
                <div className="button-row">
                  <button className="primary" onClick={saveProvider}>Save provider</button>
                  <button onClick={() => openSystemProvider(providerDraft.id)}>เปิด Chrome ที่ใช้อยู่</button>
                  <button onClick={() => openProvider(providerDraft.id)}>เปิด profile แยกสำหรับ login</button>
                </div>
              </>}
            </article>
          </section>
        )}

        {tab === "history" && (
          <section className="panel table-wrap">
            <div className="panel-title"><div><p className="eyebrow">LOCAL RECORDS</p><h2>Prompt job history</h2></div></div>
            <table><thead><tr><th>Job</th><th>Prompt</th><th>Status</th><th>Output file</th></tr></thead><tbody>
              {[...snapshot.jobs].reverse().map((job) => <tr key={job.id}><td>#{job.jobNo}</td><td>{job.prompt.slice(0, 120)}</td><td><span className="table-status">{job.status}</span></td><td>{job.outputFilePath ?? "-"}</td></tr>)}
            </tbody></table>
          </section>
        )}

        {tab === "sql" && (
          <section className="panel form-panel sql-panel">
            <div className="panel-title">
              <div><p className="eyebrow">LOCAL PERSISTENCE</p><h2>Database settings</h2></div>
              <span>{persistenceMode}</span>
            </div>
            <p className="muted">Choose where ImagePilot mirrors project and history data. Local JSON always remains available. SQLite is recommended for other machines because it uses one local file and needs no database server.</p>
            <Field label="Database mode">
              <select value={persistenceMode} onChange={(event) => setPersistenceMode(event.target.value)}>
                <option value="Json">Local JSON only - no database install</option>
                <option value="Sqlite">SQLite file - portable local database</option>
                <option value="SqlServer">SQL Server - optional server mirror</option>
              </select>
            </Field>
            <div className="button-row"><button className="primary" onClick={() => savePersistenceMode()}>Save database mode</button></div>

            {persistenceMode === "Json" && (
              <div className="database-card">
                <strong>Local JSON mode</strong>
                <p>No database server is required. ImagePilot stores local data in the workspace `data` folder. This is the simplest mode for personal use.</p>
              </div>
            )}

            {persistenceMode === "Sqlite" && (
              <div className="database-card">
                <strong>SQLite file mode</strong>
                <p>SQLite stores the mirror database as one `.db` file. Leave the path empty to use the default `data/imagepilot.db` file.</p>
                <Field label="SQLite database path"><input placeholder="Leave empty for data/imagepilot.db" value={sqlite.databasePath} onChange={(event) => setSqlite({ ...sqlite, databasePath: event.target.value })} /></Field>
                <p className="field-help">Schema: {sqlite.schemaInitialized ? "Ready" : "Not initialized yet"}{sqlite.lastSyncedAt ? ` · Last sync: ${new Date(sqlite.lastSyncedAt).toLocaleString()}` : ""}</p>
                <div className="button-row"><button onClick={testSqlite}>Test SQLite file</button><button onClick={saveSqlite}>Save SQLite settings</button><button className="primary" onClick={initializeSqlite}>Initialize SQLite</button></div>
              </div>
            )}

            {persistenceMode === "SqlServer" && (
              <div className="database-card">
                <div className="panel-title"><div><p className="eyebrow">OPTIONAL SERVER</p><h3>SQL Server connection</h3></div><span>{sql.passwordSaved ? "Password stored securely" : "No saved password"}</span></div>
                <div className="two-cols">
                  <Field label="Host"><input value={sql.host} onChange={(event) => setSql({ ...sql, host: event.target.value })} /></Field>
                  <Field label="Port"><input type="number" value={sql.port} onChange={(event) => setSql({ ...sql, port: Number(event.target.value) })} /></Field>
                  <Field label="Instance name"><input value={sql.instanceName} onChange={(event) => setSql({ ...sql, instanceName: event.target.value })} /></Field>
                  <Field label="Database name"><input value={sql.databaseName} onChange={(event) => setSql({ ...sql, databaseName: event.target.value })} /></Field>
                  <Field label="Authentication"><select value={sql.authenticationMode} onChange={(event) => setSql({ ...sql, authenticationMode: event.target.value })}><option value="Windows">Windows Authentication</option><option value="SqlServer">SQL Server Authentication</option></select></Field>
                  <Field label="Username"><input value={sql.username} onChange={(event) => setSql({ ...sql, username: event.target.value })} /></Field>
                  <Field label="Password"><input type="password" placeholder={sql.passwordSaved ? "Stored in Windows Credential Manager" : ""} value={sqlPassword} onChange={(event) => setSqlPassword(event.target.value)} /></Field>
                </div>
                <label className="check"><input type="checkbox" checked={sql.encrypt} onChange={(event) => setSql({ ...sql, encrypt: event.target.checked })} /> Encrypt connection</label>
                <label className="check"><input type="checkbox" checked={sql.trustServerCertificate} onChange={(event) => setSql({ ...sql, trustServerCertificate: event.target.checked })} /> Trust server certificate</label>
                <p className="field-help">Schema: {sql.schemaInitialized ? "Ready" : "Not initialized yet"}</p>
                <div className="button-row"><button onClick={testSql}>Test connection</button><button onClick={saveSql}>Save SQL Server settings</button><button className="primary" onClick={initializeSql}>Initialize SQL Server</button></div>
              </div>
            )}
          </section>
        )}

        {tab === "prompt-ai" && (
          <section className="panel form-panel sql-panel">
            <div className="panel-title"><div><p className="eyebrow">EXTERNAL PROMPT ASSISTANT</p><h2>Prompt AI API</h2></div><span>{promptAi.apiKeySaved ? "API key stored securely" : "No saved API key"}</span></div>
            <p className="muted">Connect an OpenAI-compatible external API. The key is stored in Windows Credential Manager and is never returned to the browser.</p>
            <label className="check"><input type="checkbox" checked={promptAi.enabled} onChange={(event) => setPromptAi({ ...promptAi, enabled: event.target.checked })} /> Enable external AI prompt improvement</label>
            <Field label="Provider"><select value={promptAi.provider} onChange={(event) => changePromptAiProvider(event.target.value)}>{promptAiProviders.map((provider) => <option value={provider.id} key={provider.id}>{provider.title}</option>)}</select></Field>
            <Field label="Base URL"><input placeholder="https://api.example.com/v1" value={promptAi.baseUrl} onChange={(event) => setPromptAi({ ...promptAi, baseUrl: event.target.value })} /></Field>
            <div className="two-cols">
              <Field label="Model">{promptAi.provider === "Gemini"
                ? <select value={promptAi.model} onChange={(event) => setPromptAi({ ...promptAi, model: event.target.value })}>{promptAiProviders[0].models.map((model) => <option value={model} key={model}>{model}</option>)}</select>
                : <input placeholder="Enter the provider model name" value={promptAi.model} onChange={(event) => setPromptAi({ ...promptAi, model: event.target.value })} />}</Field>
              <Field label="Temperature"><input type="number" min="0" max="2" step="0.1" value={promptAi.temperature} onChange={(event) => setPromptAi({ ...promptAi, temperature: Number(event.target.value) })} /></Field>
            </div>
            <Field label="API key"><input type="password" placeholder={promptAi.apiKeySaved ? "Stored in Windows Credential Manager" : "Paste API key"} value={promptAiApiKey} onChange={(event) => setPromptAiApiKey(event.target.value)} /></Field>
            <div className="button-row"><button onClick={testPromptAi}>Test connection</button><button className="primary" onClick={savePromptAi}>Save API settings</button></div>
          </section>
        )}
      </main>
      {folderList && (
        <div className="modal-backdrop" role="presentation">
          <section className="folder-modal" role="dialog" aria-modal="true" aria-labelledby="folder-picker-title">
            <div className="panel-title"><div><p className="eyebrow">LOCAL FOLDER EXPLORER</p><h2 id="folder-picker-title">Choose output folder</h2></div><button type="button" onClick={() => setFolderList(undefined)}>Close</button></div>
            <div className="folder-path">{folderList.currentPath}</div>
            <div className="folder-toolbar">
              <button type="button" disabled={!folderList.parentPath} onClick={() => openFolder(folderList.parentPath)}>Up one level</button>
              {folderList.drives.map((drive) => <button type="button" key={drive.path} onClick={() => openFolder(drive.path)}>{drive.name}</button>)}
            </div>
            <div className="new-folder-row">
              <input placeholder="New folder name" value={newFolderName} onChange={(event) => setNewFolderName(event.target.value)} onKeyDown={(event) => { if (event.key === "Enter") createFolder(); }} />
              <button type="button" onClick={createFolder} disabled={!newFolderName.trim()}>New folder</button>
            </div>
            {folderMessage && <p className="folder-message">{folderMessage}</p>}
            <div className="folder-list">
              {folderList.folders.length === 0 && <p className="muted">No subfolders. You can select this folder.</p>}
              {folderList.folders.map((folder) => <button type="button" key={folder.path} onClick={() => openFolder(folder.path)}><strong>{folder.name}</strong><span>{folder.path}</span></button>)}
            </div>
            <div className="button-row"><button className="primary" type="button" onClick={useCurrentFolder}>Use this folder</button><button type="button" onClick={() => setFolderList(undefined)}>Cancel</button></div>
          </section>
        </div>
      )}
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return <label className="field"><span>{label}</span>{children}</label>;
}

function Stat({ label, value, tone = "violet" }: { label: string; value: number; tone?: string }) {
  return <article className={`stat-card stat-${tone}`}><p>{label}</p><strong>{String(value).padStart(2, "0")}</strong></article>;
}
