import { Component, Input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { clubContent } from '../../content/club-content';

@Component({
  selector: 'app-public-heading',
  template: `<section class="public-page-hero"><div class="page-hero-inner"><span class="eyebrow">{{ eyebrow }}</span><h1>{{ title }}</h1><p>{{ description }}</p></div></section>`,
})
export class PublicHeading {
  @Input({ required: true }) title = ''; @Input() eyebrow = "ES'HLAHLENI SOCIAL CLUB"; @Input() description = '';
}

@Component({
  imports: [PublicHeading, RouterLink],
  template: `<app-public-heading title="YOUR KIND OF SPACE." eyebrow="THE CLUB / EST. 2025" description="Get to know the name behind the badge." />
    <section class="section split-content">
      <div class="story-emblem"><img src="assets/branding/eshlahleni-logo.jpeg" alt="The original Es’hlahleni Social Club emblem, established 2025" width="400" height="447" /><span class="eyebrow">KUPHOLIWE AK’TATAZELWA!</span></div>
      <div><span class="section-tag">OUR IDENTITY</span><h2>ONE BADGE.<br />A SHARED SENSE<br />OF BELONGING.</h2><p class="lead">Es'hlahleni is a cannabis-centred social club and hangout spot, established in 2025.</p><p>Come through for cannabis, edibles, cookies, PS5 and good company. Settle in, relax and enjoy the space. The badge is unmistakably Es'hlahleni.</p><p>Apply here. Club administrators review applications and issue a member number when approved.</p><div class="adult-note"><strong>18+ only</strong><p>You must be at least 18 to register.</p></div><a class="button" routerLink="/membership">Explore membership <span aria-hidden="true">↗</span></a></div>
    </section>`,
})
export class AboutPage { readonly content = clubContent; }

@Component({
  imports: [PublicHeading, RouterLink],
  template: `<app-public-heading title="YOUR WAY INTO THE CLUB." eyebrow="MEMBERSHIP / 18+" description="Apply online and let the club review your application." />
    <section class="section"><div class="section-head"><div><span class="section-tag">HOW IT WORKS</span><h2>FOUR STEPS.<br />ONE FIRST MOVE.</h2></div><p class="muted membership-intro">You don’t need an account to apply.<br />Just your details, your consent and a moment of your time.</p></div>
      <ol class="membership-steps"><li><span>01</span><div><h3>Application</h3><p>Submit your name, date of birth and contact details. Read the membership terms and privacy notice before you apply.</p></div></li><li><span>02</span><div><h3>Pending review</h3><p>Your application will be reviewed by an administrator.</p></div></li><li><span>03</span><div><h3>Decision</h3><p>An administrator accepts or declines the application.</p></div></li><li><span>04</span><div><h3>Member</h3><p>Approved applicants become active members and receive a member number.</p></div></li></ol>
      <div class="membership-requirements"><div><span class="section-tag">BEFORE YOU APPLY</span><h2>THE ESSENTIALS.</h2></div><ul><li>You must be 18 years of age or older.</li><li>Have your date of birth, email and international-format phone number ready.</li><li>Read and accept the club’s membership terms and privacy notice.</li><li>Keep the application reference shown after submission.</li></ul></div>
      <div class="button-group"><a class="button" routerLink="/join">Register for membership <span aria-hidden="true">↗</span></a><a class="text-link" routerLink="/faq">Questions? Start here →</a></div>
    </section>`,
})
export class MembershipPage { readonly content = clubContent; }

@Component({
  imports: [PublicHeading, RouterLink],
  template: `<app-public-heading title="GOOD TO KNOW." eyebrow="REGISTRATION & FREQUENTLY ASKED QUESTIONS" description="A little clarity before you become part of the club." />
    <section class="section split-content faq-layout"><div><span class="section-tag">THE SHARED SPACE</span><h2>RESPECT THE CLUB.<br />RESPECT EACH OTHER.</h2><div class="rules-list">@for (rule of content.rules; track rule.title) { <article><span class="rule-index">0{{ $index + 1 }}</span><div><h3>{{ rule.title }}</h3><p>{{ rule.text }}</p></div></article> }</div></div>
      <div><span class="section-tag">A FEW ANSWERS</span><h2>YOUR QUESTIONS.</h2><div class="faq-list">
        <details><summary>Who can apply for membership?</summary><p>You must be at least 18. Your date of birth is checked when you register.</p></details>
        <details><summary>Do I need to create an account?</summary><p>No. You can submit a membership application without creating an account or choosing a password. This website does not provide applicant or member login accounts.</p></details>
        <details><summary>Am I a member once I submit?</summary><p>Your application is pending until an administrator accepts it.</p></details>
        <details><summary>When do I receive my member number?</summary><p>Your member number is issued when an administrator accepts your application.</p></details>
        <details><summary>Can I track my application online?</summary><p>There is no public application-tracking page. Keep the reference shown after submission for any enquiry with the club.</p></details>
        <details><summary>Can I order cannabis on this website?</summary><p>No. This website provides club information and membership applications. It has no product catalogue, shopping cart, checkout or delivery-ordering service.</p></details>
      </div><a class="text-link faq-contact" routerLink="/contact">Still have a question? Get in touch ↗</a></div>
    </section>`,
})
export class FaqPage { readonly content = clubContent; }

@Component({
  imports: [RouterLink],
  styleUrl: './contact.scss',
  template: `
    <section class="connect" aria-labelledby="connect-title">
      <header class="connect-heading"><span class="eyebrow">CONTACT / ES'HLAHLENI</span><h1 id="connect-title">LET’S CONNECT.</h1><p>Questions about the club or your membership application? Start here.</p></header>
      <div class="connect-grid">
        <section class="connect-place" aria-labelledby="place-title"><span class="eyebrow">FIND THE CLUB</span><h2 id="place-title">OUR SPACE.</h2><address>@for (line of content.contact.address; track line) { <span>{{ line }}</span> }</address></section>
        <section class="connect-chat" aria-labelledby="chat-title"><span class="eyebrow">GET IN TOUCH</span><h2 id="chat-title">SAY HELLO.</h2><span class="connect-label">WHATSAPP</span><a class="connect-number" [href]="content.contact.whatsAppUrl">{{ content.contact.whatsAppNumber }}</a><a class="button" [href]="content.contact.whatsAppUrl">Chat on WhatsApp <span aria-hidden="true">↗</span></a></section>
      </div>
      <div class="connect-apply"><p>Ready to take the first step?</p><a class="button" routerLink="/join">Register for membership <span aria-hidden="true">↗</span></a></div>
    </section>`,
})
export class ContactPage { readonly content = clubContent; }

@Component({
  imports: [PublicHeading, RouterLink],
  template: `<app-public-heading title="MEMBERSHIP TERMS." eyebrow="APPLICATION / SAMPLE TERMS" description="Read before you apply." />
    <section class="section policy-copy"><div class="alert alert--preview"><strong>Illustrative terms — awaiting club approval</strong><p>This is preview content, not the club’s final published membership agreement. The club must confirm its terms before public launch.</p></div><h2>THE APPLICATION PROCESS.</h2><p>Registration requires you to be at least 18. Provide accurate names, date of birth and contact details.</p><p>Apply online. An administrator reviews your application and issues a member number upon acceptance.</p><p>Keep the application reference provided after submission. Do not repeatedly submit the same contact details while registration awaits completion.</p><p>This website does not collect payment.</p><a class="text-link" routerLink="/privacy">Read the privacy notice →</a><div class="policy-back"><a class="button" routerLink="/join">Return to application ↗</a></div></section>`,
})
export class TermsPage {}

@Component({
  imports: [PublicHeading, RouterLink],
  template: `<app-public-heading title="YOUR INFORMATION." eyebrow="APPLICATION / SAMPLE PRIVACY NOTICE" description="What the membership application asks for and why." />
    <section class="section policy-copy"><div class="alert alert--preview"><strong>Preview notice — awaiting club approval</strong><p>The club’s final privacy policy and retention arrangements must be confirmed before public launch. This page describes the current application flow.</p></div><h2>ONLY THE DETAILS<br />NEEDED TO APPLY.</h2><p>The form collects your first name, surname, date of birth, phone number, email address and your terms/privacy consent. Your date of birth is used to check the minimum age.</p><p>Registrations are saved by the club’s membership service. Authorised staff review each application and create a member record when it is accepted. Your registration reference is shown after submission.</p><p>This website does not ask for a national ID number, passport, bank details or medical information. There is no applicant account or password to create.</p><p>Contact the club for questions about your information.</p><a class="text-link" routerLink="/membership-terms">Read the membership terms →</a><div class="policy-back"><a class="button" routerLink="/join">Return to application ↗</a></div></section>`,
})
export class PrivacyPage {}

@Component({
  imports: [RouterLink],
  template: `<section class="section not-found"><span class="eyebrow">A LITTLE OFF THE PATH</span><div class="not-found-number" aria-hidden="true">404</div><h1>This page isn’t here.</h1><p>The link may have changed. Let’s get you back to the club.</p><a class="button" routerLink="/">Back to the home page ↗</a></section>`,
})
export class NotFoundPage {}
