export async function copyText(text) {
  await navigator.clipboard.writeText(text);
}

export function downloadTextFile(fileName, text, contentType = "application/x-ndjson;charset=utf-8") {
  const blob = new Blob([text], { type: contentType });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}
