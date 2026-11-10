// Supabase Edge Function. Secrets remain in the function environment.
Deno.serve(async (request: Request) => {
  if (request.method !== 'POST') return new Response('Method not allowed', {status: 405});
  const authorization = request.headers.get('authorization');
  if (!authorization?.startsWith('Bearer ')) return new Response('Sign in required', {status: 401});
  const url = Deno.env.get('SUPABASE_URL');
  const key = Deno.env.get('SUPABASE_SERVICE_ROLE_KEY');
  if (!url || !key) return new Response('Service unavailable', {status: 503});
  const verified = await fetch(`${url}/auth/v1/user`, {headers: {apikey: key, authorization}});
  if (!verified.ok) return new Response('Sign in required', {status: 401});
  const user = await verified.json();
  if (typeof user.id !== 'string' || !/^[0-9a-f-]{36}$/i.test(user.id)) return new Response('Invalid identity', {status: 401});
  const result = await fetch(`${url}/auth/v1/admin/users/${user.id}`, {method: 'DELETE', headers: {apikey: key, authorization: `Bearer ${key}`}});
  return new Response(result.ok ? '{}' : 'Account deletion failed', {status: result.ok ? 200 : 502, headers: {'Content-Type': 'application/json'}});
});
