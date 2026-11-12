import { Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';

@Injectable()
export class ClubTitleStrategy extends TitleStrategy {
  private readonly titleService = inject(Title);
  private readonly meta = inject(Meta);
  override updateTitle(snapshot: RouterStateSnapshot): void {
    let route = snapshot.root;
    while (route.firstChild) route = route.firstChild;
    const title = `${this.buildTitle(snapshot) ?? 'A place to belong'} | Es'hlahleni Social Club`;
    const description = route.data['description'] ?? 'Cannabis, PS5 and good company in Durban Central. Discover Es’hlahleni Social Club and start your membership registration. 18+.';
    const noIndex = snapshot.url.startsWith('/admin') || route.data['noIndex'];
    this.titleService.setTitle(title);
    this.meta.updateTag({ name: 'description', content: description });
    this.meta.updateTag({ name: 'robots', content: noIndex ? 'noindex, nofollow' : 'index, follow' });
    this.meta.updateTag({ property: 'og:title', content: title });
    this.meta.updateTag({ property: 'og:description', content: description });
    this.meta.updateTag({ property: 'og:type', content: 'website' });
    this.meta.updateTag({ property: 'og:site_name', content: "Es'hlahleni Social Club" });
  }
}
