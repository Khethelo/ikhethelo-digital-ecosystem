import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { clubContent } from '../../content/club-content';

@Component({
  imports: [RouterLink],
  template: `
    <section class="public-hero" aria-labelledby="home-title">
      <div class="hero-inner">
        <div class="hero-copy">
          <span class="eyebrow"><span class="tiny-dot" aria-hidden="true"></span> DURBAN · EST. 2025</span>
          <h1 id="home-title">Come through.<br />Kick back.<br /><span>Good vibes.</span></h1>
          <p class="hero-lead">KUPHOLIWE AK’TATAZELWA!</p>
          <div class="hero-actions"><a class="button button--olive" routerLink="/join">Register for membership <span aria-hidden="true">↗</span></a><a class="hero-secondary" routerLink="/about">Meet the club <span aria-hidden="true">→</span></a></div>
          <p class="hero-adult"><span class="adult-pill">18+</span> Register online. Complete the R100 membership step at the club.</p>
        </div>
        <div class="hero-art">
          <span class="emblem-corner emblem-corner--top">CANNABIS. PS5. GOOD VIBES.</span>
          <div class="crest-wrap"><img class="hero-emblem" src="assets/branding/eshlahleni-logo.jpeg" alt="Es’hlahleni Social Club black-and-white emblem: Kupholiwe Ak’tatazelwa! Established 2025." width="933" height="1043" fetchpriority="high" /></div>
          <div class="hero-art-caption"><span class="tiny-dot" aria-hidden="true"></span> Your kind of space.</div>
        </div>
      </div>
      <div class="hero-footnote"><span>KUPHOLIWE AK’TATAZELWA!</span><a href="#the-vibe" class="hero-scroll">Take a look around <span aria-hidden="true">↓</span></a></div>
    </section>

    <section id="the-vibe" class="section vibe-section" aria-labelledby="vibe-title">
      <div class="section-head"><div><span class="section-tag">A LITTLE MORE ES’HLAHLENI</span><h2 id="vibe-title">More than a weed shop.<br />It's a place to socialize.</h2></div><p class="section-description">Settle in. Catch up. Enjoy the moment.<br />There’s more to the club than the first hello.</p></div>
      <div class="experience-grid">
        <article class="experience-card"><span class="experience-number">01 / THE CHILL</span><div class="experience-icon" aria-hidden="true"><svg viewBox="0 0 48 48"><path d="M9 28v10m30-10v10M12 29V17a6 6 0 0 1 6-6h12a6 6 0 0 1 6 6v12M9 25a4 4 0 0 1 8 0v5h14v-5a4 4 0 0 1 8 0v9H9Z" /></svg></div><h3>Find your chill.</h3><p>A relaxed space to sit back, catch up with good people and let the day slow down.</p></article>
        <article class="experience-card"><span class="experience-number">02 / THE PLAY</span><div class="experience-icon" aria-hidden="true"><svg viewBox="0 0 48 48"><path d="M16 15h16c5 0 8 17 6 19-3 3-8-5-10-5h-8c-2 0-7 8-10 5-2-2 1-19 6-19Z"/><path d="M15 21h8m-4-4v8m11-4h.1m4 4h.1" /></svg></div><h3>Make it a game.</h3><p>PS5 at the venue. A little friendly competition, a few laughs, and a reason to hang out.</p></article>
        <article class="experience-card"><span class="experience-number">03 / THE EXPERIENCE</span><div class="experience-icon experience-icon--leaf" aria-hidden="true"><img src="assets/branding/eshlahleni-favicon.png" alt="" width="48" height="48" /></div><h3>Your kind of vibe.</h3><p>Cannabis, edibles, cookies and other cannabis-related products are part of the Es’hlahleni experience.</p></article>
      </div>
    </section>

    <section class="process-section" aria-labelledby="process-title"><div class="section">
      <div class="section-head"><div><span class="section-tag">MAKE YOURSELF AT HOME</span><h2 id="process-title">A few details.<br />Then come through.</h2></div><a class="text-link" routerLink="/membership">How membership works <span aria-hidden="true">↗</span></a></div>
      <div class="process-grid">
        <article class="process-step"><div class="step-top"><span>01 / REGISTER</span><span aria-hidden="true">↗</span></div><h3>Start right here.</h3><p>Share your details and consent online. We’ll save your registration and show your confirmation reference. No account needed.</p></article>
        <article class="process-step"><div class="step-top"><span>02 / VISIT</span><span aria-hidden="true">↗</span></div><h3>Meet us at the club.</h3><p>Visit Es’hlahleni and complete the R100 membership step in person. Your online form is the first step.</p></article>
        <article class="process-step"><div class="step-top"><span>03 / ACTIVATE</span><span aria-hidden="true">✓</span></div><h3>Welcome in.</h3><p>Staff activate your membership and issue your member number.</p></article>
      </div>
    </div></section>

    <section class="section visit-section" aria-labelledby="visit-title"><div class="visit-card"><div><span class="section-tag">FIND YOUR WAY HERE</span><h2 id="visit-title">Good company.<br />Right here in Durban.</h2><p>{{ content.contact.address.join(', ') }}</p><div class="button-group"><a class="button" [href]="content.contact.directionsUrl" target="_blank" rel="noopener noreferrer">Get directions <span aria-hidden="true">↗</span></a><a class="text-link" [href]="content.contact.whatsAppUrl">Chat on WhatsApp <span aria-hidden="true">→</span></a></div></div><div class="visit-note"><span class="visit-note-label">YOUR NEXT MOVE</span><p>Come for the vibe.<br />Stay for the company.</p><a routerLink="/join">Start your registration <span aria-hidden="true">↗</span></a></div></div></section>
    <section class="membership-banner"><div><span class="eyebrow">WE’LL SEE YOU AT THE CLUB</span><h2>Good vibes.<br />Your first step starts here.</h2><p>18+. Register online. Complete your registration at the club.</p></div><a class="button button--olive" routerLink="/join">Register for membership <span aria-hidden="true">↗</span></a><img class="banner-leaf" src="assets/branding/eshlahleni-favicon.png" alt="" width="240" height="240" loading="lazy" /></section>
  `,
})
export class HomePage { readonly content = clubContent; }
