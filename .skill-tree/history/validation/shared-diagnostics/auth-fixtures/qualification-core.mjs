// Pure measurements shared by the real-browser harness and negative controls.
export function composite(front, back) {
  const alpha = front[3] ?? 1;
  return front.slice(0, 3).map((v, i) => v * alpha + back[i] * (1 - alpha)).concat(1);
}
export function contrast(first, second) {
  const luminance = c => c.slice(0, 3).map(v => v / 255).map(v => v <= .04045 ? v / 12.92 : ((v + .055) / 1.055) ** 2.4)
    .reduce((sum, v, i) => sum + v * [.2126, .7152, .0722][i], 0);
  const a = luminance(first), b = luminance(second);
  return (Math.max(a, b) + .05) / (Math.min(a, b) + .05);
}
export function threshold(fontSize, fontWeight) { return fontSize >= 24 || fontSize >= 18.6666667 && fontWeight >= 700 ? 3 : 4.5; }
export function geometry({width, scrollWidth, controls}) {
  const errors = [];
  if (scrollWidth > width + 1) errors.push({kind:'overflow', width, scrollWidth});
  for (let i = 0; i < controls.length; i++) {
    const a = controls[i];
    if (!a.scrollContainer && (a.left < -1 || a.right > width + 1)) errors.push({kind:'control-bounds', id:a.id});
    if (a.primary && (a.height < 44 || a.width < 44)) errors.push({kind:'primary-target',id:a.id,height:a.height,width:a.width});
    for (const b of controls.slice(i + 1)) {
      if (a.contains?.includes(b.id) || b.contains?.includes(a.id)) continue;
      if (Math.min(a.right,b.right) - Math.max(a.left,b.left) > 1 && Math.min(a.bottom,b.bottom) - Math.max(a.top,b.top) > 1)
        errors.push({kind:'overlap',ids:[a.id,b.id]});
    }
  }
  return errors;
}
