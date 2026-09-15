import { customElement, html } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';

@customElement('reindex-detail-box')
export default class ReindexDetailBox extends UmbLitElement {
 
  constructor() {
    super();   
  }

  override render() {
    return html`
      <uui-box>
        <p>Implement this/p>
      </uui-box>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    "reindex-detail-box": ReindexDetailBox;
  }
}