// k6 external HTTP load; use pre-provisioned disposable fixture tokens. NEVER send credentials/tokens to source control.
import http from 'k6/http';
import { check } from 'k6';
import { Trend, Rate } from 'k6/metrics';
const permissionRead = new Trend('permission_refresh_ms', true);
const success = new Rate('authorization_expected_status');
const tokens = JSON.parse(open(__ENV.TOKENS_FILE));
if (!Array.isArray(tokens) || tokens.length === 0) throw new Error('TOKENS_FILE must be a JSON array of test bearer tokens');
if (!__ENV.BASE_URL || !__ENV.TARGET_PATH) throw new Error('BASE_URL and TARGET_PATH required');
export const options = { vus: Number(__ENV.VUS || 32), duration: __ENV.DURATION || '60s', summaryTrendStats: ['avg','med','p(50)','p(95)','p(99)','max'], thresholds: { authorization_expected_status: ['rate==1'] } };
export default function () {
  const token = tokens[(__VU - 1) % tokens.length];
  const r = http.get(__ENV.BASE_URL + __ENV.TARGET_PATH, { headers: { Authorization: 'Bearer ' + token }, tags: { operation: 'exact-permission-check' } });
  success.add(check(r, { expected: r => r.status === Number(__ENV.EXPECTED_STATUS || 200) }));
  const refresh = http.get(__ENV.BASE_URL + '/api/Auth/me/permissions', { headers: { Authorization: 'Bearer ' + token } });
  permissionRead.add(refresh.timings.duration);
}
