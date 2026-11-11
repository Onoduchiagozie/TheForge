import { chromium } from 'playwright';
import { mkdirSync } from 'node:fs';

const base = 'http://127.0.0.1:5157/';
const query = 'Recount the duel between Fulgrim and Ferrus Manus at Isstvan V.';
const output = 'Tests/BrowserSmoke/artifacts';
mkdirSync(output, { recursive: true });
const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1280, height: 840 } });
const errors = [];
const failedRequests = [];
const badResponses = [];
page.on('pageerror', err => errors.push(err.message));
page.on('console', msg => { if (msg.type() === 'error') errors.push(msg.text()); });
page.on('requestfailed', req => failedRequests.push(req.url() + ' ' + (req.failure()?.errorText ?? '')));
page.on('response', resp => { if (resp.status() >= 400) badResponses.push(resp.status() + ' ' + resp.url()); });

try {
  await page.goto(base, { timeout: 60_000, waitUntil: 'domcontentloaded' });
  await page.locator('.workspace').waitFor({ timeout: 60_000 });
  await page.waitForTimeout(2500); // Allow interactive server circuit to attach
  console.log('Blazor script present:', await page.evaluate(() => typeof window.Blazor !== 'undefined'));
  const narrator = page.locator('.mode-btn').filter({ hasText: 'Narrator' });
  await narrator.click({ timeout: 30_000 });
  await page.waitForTimeout(900);
  const narratorClass = await narrator.getAttribute('class');
  console.log('Narrator button class after click:', narratorClass);
  if (!narratorClass?.includes('mode-btn--active'))
    throw new Error('Blazor did not process Narrator button click');
  await page.locator('.submode-toggle__btn').filter({ hasText: 'WIDE NET' }).click();
  await page.locator('.query-input').fill(query);
  const healthText = await page.locator('.status-item--health').innerText();
  console.log('Health:', healthText);

  await page.locator('.submit-btn').click();
  console.log('Query started:', query);
  await page.waitForFunction(() =>
    !!document.querySelector('.complete-mark') ||
    !!document.querySelector('.response-error') ||
    !!document.querySelector('.interrupted'), {}, { timeout: 360_000 });

  const err = page.locator('.response-error');
  if (await err.count()) throw new Error('Frontend stream ERROR: ' + await err.innerText());
  const text = await page.locator('.response-text').first().innerText();
  const sourceCount = await page.locator('.source-card').count();
  const stopVisible = await page.locator('.stop-btn').count();
  const submitEnabled = await page.locator('.submit-btn').isEnabled();

  console.log('Answer characters:', text.length);
  console.log('Source cards:', sourceCount);
  console.log('Stop button visible:', !!stopVisible);
  console.log('Submit enabled:', submitEnabled);
  console.log('Answer first 180 characters:', text.slice(0, 180).replace(/\s+/g, ' '));
  console.log('Page errors:', errors);

  if (text.trim().length < 100) throw new Error('Answer missing or too short');
  if (sourceCount < 1) throw new Error('No source cards rendered');
  if (stopVisible) throw new Error('Frontend still in Streaming state after DONE');
  if (!submitEnabled) throw new Error('Submit remains disabled after completion');

  console.log('PASS: Browser completed a real non-Horus streamed Oracle narration.');
} finally {
  console.log('JavaScript/console errors:', errors.slice(-15));
  console.log('Failed network requests:', failedRequests.slice(-15));
  console.log('HTTP >=400:', badResponses.slice(-15));
  await page.screenshot({ path: output + '/forge-stream-final.png', fullPage: true }).catch(() => {});
  await browser.close();
}
