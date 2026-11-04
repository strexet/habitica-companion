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

// Unlike composite(), sourceOver also retains a translucent backing layer.
export function sourceOver(front, back) {
  const fa=front[3]??1, ba=back[3]??1, alpha=fa+ba*(1-fa);
  return front.slice(0,3).map((v,i)=>alpha?(v*fa+back[i]*ba*(1-fa))/alpha:0).concat(alpha);
}

// Layers are root-to-leaf. CSS opacity applies to the painted group, including its
// background, before that group is composited over the parent (CSS Color4 §3.3).
export function assessDisabledText(foreground, layers, minimum=3) {
  let ink=foreground.slice(), surface=[0,0,0,0];
  for(const {background,opacity} of [...layers].reverse()) {
    if(!Number.isFinite(opacity)||opacity<0||opacity>1)throw Error('Unqualified group opacity');
    ink=sourceOver(ink,background);surface=sourceOver(surface,background);
    ink[3]*=opacity;surface[3]*=opacity;
  }
  const ratio=contrast(composite(ink,[255,255,255,1]),composite(surface,[255,255,255,1]));
  return {ratio,minimum,passed:ratio>=minimum};
}

// Pixel evidence compares text-only ink against a full-width reference in the
// same engine/font/value. Neither input.value nor outer bounds establish a pass.
export function assessNativeDate({actualInk,referenceInk,evidenceAvailable}, minimumRetention=.97) {
  if(!evidenceAvailable||!Number.isFinite(actualInk)||!Number.isFinite(referenceInk)||referenceInk<100||actualInk<0)
    return {qualification:'unavailable',kind:'native-date-evidence',reason:'Usable native text pixel evidence is required'};
  const retention=actualInk/referenceInk;
  return {qualification:retention>=minimumRetention?'pass':'fail',kind:'native-date-clipping',retention,minimumRetention,actualInk,referenceInk};
}

export function headerClearance({header,main,content}) {
  const errors=[];
  const overlaps=rect=>Math.min(rect.right,header.right)-Math.max(rect.left,header.left)>1
    &&Math.min(rect.bottom,header.bottom)-Math.max(rect.top,header.top)>1;
  if(!content.length)errors.push({kind:'missing-main-content'});
  if(overlaps(main))errors.push({kind:'header-landmark-overlap',headerBottom:header.bottom,mainTop:main.top});
  for(const rect of content)if(overlaps(rect))errors.push({kind:'header-content-overlap',headerBottom:header.bottom,contentTop:rect.top});
  return errors;
}

export function assessEquipmentReadability({identities,stats}) {
  const errors=[];
  for(const row of identities) {
    if(![row.width,row.availableWidth,row.wordWidth].every(Number.isFinite)||row.width<=0||row.availableWidth<=0||row.wordWidth<=0||(['action-label','count-label'].includes(row.kind)&&(!Array.isArray(row.wordLineCounts)||!row.wordLineCounts.length||row.wordLineCounts.some(n=>!Number.isInteger(n)||n<1))))
      errors.push({...row,sampleKind:row.kind,kind:'equipment-name-unavailable'});
    else if(row.width<(['action-label','count-label'].includes(row.kind)?row.wordWidth:Math.min(row.availableWidth,row.wordWidth))-2||(['action-label','count-label'].includes(row.kind)&&row.wordLineCounts.some(n=>n!==1)))errors.push({...row,sampleKind:row.kind,kind:'equipment-name-column'});
  }
  for(const row of stats) {
    if(![row.width,row.inkWidth,row.lineCount].every(Number.isFinite)||row.width<=0||row.inkWidth<=0||row.lineCount<1)
      errors.push({...row,sampleKind:row.kind,kind:'equipment-stat-unavailable'});
    else if(row.width<row.inkWidth-2||row.lineCount!==1)errors.push({...row,sampleKind:row.kind,kind:'equipment-stat-token'});
  }
  return errors;
}
