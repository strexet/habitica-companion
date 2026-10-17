// Fixture-only Worker: keep the concurrency gate inside workerd service bindings.
// No production entrypoint imports this module.
let armed = null;
let held = null;
// Promises must belong to the request awaiting them; shared gates store data only.
async function waitFor(gate, field) {
 const deadline = Date.now() + 10000;
 while (!gate[field]) {
  if (Date.now() >= deadline) throw new Error('fixture_gate_deadline');
  await scheduler.wait(2);
 }
}
export default {
 async fetch(request, env) {
  const path = new URL(request.url).pathname;
  if (path === '/__gate/arm' && request.method === 'POST') {
   if (armed || held) return new Response(null, { status: 409 });
   armed = { entered: false, released: false }; return new Response(null, { status: 204 });
  }
  if (path === '/__gate/entered') {
   const gate = armed ?? held;
   if (!gate) return new Response(null, { status: 409 });
   await waitFor(gate, 'entered'); return new Response(null, { status: 204 });
  }
  if (path === '/__gate/release' && request.method === 'POST') {
   const gate = held ?? armed; held = armed = null;
   if (gate) { gate.entered = true; gate.released = true; } return new Response(null, { status: 204 });
  }
  const response = await env.AUTH_IDENTITY.fetch(request);
  if (path !== '/epoch' || !armed) return response;
  const gate = armed; armed = null; held = gate;
  const bytes = response.status === 204 ? null : await response.arrayBuffer();
  const detached = new Response(bytes, { status: response.status, headers: response.headers });
  gate.entered = true; await waitFor(gate, 'released'); return detached;
 }
};
