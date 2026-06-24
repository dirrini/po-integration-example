import { CommonModule } from '@angular/common';
import { HttpClient, provideHttpClient } from '@angular/common/http';
import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { bootstrapApplication } from '@angular/platform-browser';

interface PurchaseOrder {
  projectExternalCode: string;
  poNumber: string;
  vendor: string;
  status: 'released';
  materialCode: string;
  materialDescription: string;
  quantity: number;
  deliveryDate: string;
}

interface GatewayResponse {
  status?: string;
  error?: string;
  systemAction?: unknown;
  [key: string]: unknown;
}

const API_BASE_URL = window.location.port === '4200' ? 'http://localhost:5000' : '';

const createEmptyOrder = (): PurchaseOrder => ({
  projectExternalCode: 'SAP-PROJ-001',
  poNumber: '',
  vendor: '',
  status: 'released',
  materialCode: '',
  materialDescription: '',
  quantity: 1,
  deliveryDate: ''
});

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div *ngIf="successToastMessage" class="success-toast" role="status" aria-live="polite">
      {{ successToastMessage }}
    </div>

    <div style="font-family: sans-serif; padding: 30px; max-width: 620px; margin: 0 auto; border: 1px solid #ccc; border-radius: 8px; box-shadow: 0 4px 6px rgba(0,0,0,0.05);">
      <h2 style="color: #2c3e50; margin-top: 0; margin-bottom: 5px;">SAP Purchase Order Intake</h2>
      <p style="font-size: 13px; color: #7f8c8d; margin-bottom: 20px;">Architecture: SAP Interface -> API Gateway -> RabbitMQ -> Worker -> External System</p>

      <div style="margin-bottom: 15px;">
        <label style="display:block; margin-bottom:5px; font-weight:bold; font-size:14px;">Project Code:</label>
        <input type="text" [(ngModel)]="order.projectExternalCode" style="width:100%; padding:10px; box-sizing:border-box; border:1px solid #ccc; border-radius:4px;" placeholder="SAP-PROJ-001">
      </div>

      <div style="display:grid; grid-template-columns: 1fr 1fr; gap: 15px; margin-bottom: 15px;">
        <div>
          <label style="display:block; margin-bottom:5px; font-weight:bold; font-size:14px;">PO Number:</label>
          <input type="text" [(ngModel)]="order.poNumber" style="width:100%; padding:10px; box-sizing:border-box; border:1px solid #ccc; border-radius:4px;" placeholder="4500001234">
        </div>

        <div>
          <label style="display:block; margin-bottom:5px; font-weight:bold; font-size:14px;">Status:</label>
          <input type="text" [ngModel]="order.status" readonly style="width:100%; padding:10px; box-sizing:border-box; border:1px solid #ccc; border-radius:4px; background-color:#f4f6f8; color:#27ae60; font-weight:bold;">
        </div>
      </div>

      <div style="margin-bottom: 15px;">
        <label style="display:block; margin-bottom:5px; font-weight:bold; font-size:14px;">Vendor:</label>
        <input type="text" [(ngModel)]="order.vendor" style="width:100%; padding:10px; box-sizing:border-box; border:1px solid #ccc; border-radius:4px;" placeholder="Vendor ABC Ltda">
      </div>

      <div style="display:grid; grid-template-columns: 1fr 1fr; gap: 15px; margin-bottom: 15px;">
        <div>
          <label style="display:block; margin-bottom:5px; font-weight:bold; font-size:14px;">Material Code:</label>
          <input type="text" [(ngModel)]="order.materialCode" style="width:100%; padding:10px; box-sizing:border-box; border:1px solid #ccc; border-radius:4px;" placeholder="MAT-100293">
        </div>

        <div>
          <label style="display:block; margin-bottom:5px; font-weight:bold; font-size:14px;">Quantity:</label>
          <input type="number" min="1" [(ngModel)]="order.quantity" style="width:100%; padding:10px; box-sizing:border-box; border:1px solid #ccc; border-radius:4px;">
        </div>
      </div>

      <div style="margin-bottom: 15px;">
        <label style="display:block; margin-bottom:5px; font-weight:bold; font-size:14px;">Material Description:</label>
        <input type="text" [(ngModel)]="order.materialDescription" style="width:100%; padding:10px; box-sizing:border-box; border:1px solid #ccc; border-radius:4px;" placeholder="Industrial pump spare kit">
      </div>

      <div style="margin-bottom: 20px;">
        <label style="display:block; margin-bottom:5px; font-weight:bold; font-size:14px;">Delivery Date:</label>
        <input type="date" [(ngModel)]="order.deliveryDate" style="width:100%; padding:10px; box-sizing:border-box; border:1px solid #ccc; border-radius:4px;">
      </div>

      <button (click)="submitOrder()" style="background-color:#2980b9; color:white; padding:12px 15px; border:none; border-radius:4px; cursor:pointer; width:100%; font-size:16px; font-weight:bold; margin-bottom: 25px;">
        Send Purchase Order
      </button>

      <hr style="border: 0; border-top: 1px solid #eee; margin-bottom: 20px;" />

      <div style="background-color: #f8f9fa; padding: 15px; border-radius: 6px; border: 1px solid #e9ecef;">
        <h4 style="margin-top:0; margin-bottom:10px; color:#34495e;">Infrastructure Simulation</h4>

        <div style="display: flex; align-items: center; justify-content: space-between; gap: 16px;">
          <div>
            <strong style="display: block; font-size: 14px;">Worker Listener Daemon</strong>
            <span [style.color]="isWorkerRunning ? '#27ae60' : '#c0392b'" style="font-size: 12px; font-weight: bold;">
              Status: {{ isWorkerRunning ? 'RUNNING (Processing Active)' : 'PAUSED (Messages Will Backlog)' }}
            </span>
          </div>

          <label class="switch" style="position: relative; display: inline-block; width: 60px; height: 34px; flex: 0 0 auto;">
            <input type="checkbox" [checked]="isWorkerRunning" (change)="toggleWorkerState()" style="opacity: 0; width: 0; height: 0;">
            <span class="slider" [style.background-color]="isWorkerRunning ? '#27ae60' : '#ccc'" style="position: absolute; cursor: pointer; top: 0; left: 0; right: 0; bottom: 0; transition: .4s; border-radius: 34px;"></span>
          </label>
        </div>
      </div>

      <div *ngIf="gatewayResponse" style="margin-top:20px; padding:12px; background-color:#e8f4f8; border-left:4px solid #2980b9; font-size:14px; border-radius: 0 4px 4px 0;">
        <strong>System Output Log:</strong>
        <pre style="margin: 5px 0 0 0; font-family: monospace; font-size: 12px; white-space: pre-wrap;">{{ gatewayResponse | json }}</pre>
      </div>
    </div>
  `,
  styles: [`
    .success-toast {
      position: fixed;
      top: 20px;
      left: 50%;
      z-index: 1000;
      transform: translate(-50%, -18px);
      width: min(420px, calc(100vw - 32px));
      box-sizing: border-box;
      padding: 14px 18px;
      border: 1px solid #1f9d55;
      border-radius: 6px;
      background: #e9f9ef;
      color: #17633a;
      box-shadow: 0 10px 24px rgba(23, 99, 58, 0.18);
      font-family: sans-serif;
      font-size: 14px;
      font-weight: 700;
      line-height: 1.4;
      text-align: center;
      opacity: 0;
      animation: toast-slide-fade 3.5s ease-in-out forwards;
      will-change: transform, opacity;
    }

    @keyframes toast-slide-fade {
      0% {
        opacity: 0;
        transform: translate(-50%, -18px);
      }

      12%,
      82% {
        opacity: 1;
        transform: translate(-50%, 0);
      }

      100% {
        opacity: 0;
        transform: translate(-50%, -8px);
      }
    }

    .slider:before {
      position: absolute;
      content: "";
      height: 26px;
      width: 26px;
      left: 4px;
      bottom: 4px;
      background-color: white;
      transition: .4s;
      border-radius: 50%;
    }

    input:checked + .slider:before {
      transform: translateX(26px);
    }
  `]
})
export class AppComponent implements OnInit {
  private readonly http = inject(HttpClient);

  public order: PurchaseOrder = createEmptyOrder();
  public gatewayResponse: GatewayResponse | null = null;
  public isWorkerRunning = true;
  public successToastMessage = '';

  private successToastTimeoutId: ReturnType<typeof setTimeout> | null = null;

  public ngOnInit(): void {
    this.isWorkerRunning = true;
  }

  public submitOrder(): void {
    const payload: PurchaseOrder = {
      ...this.order,
      status: 'released'
    };

    this.http.post<GatewayResponse>(`${API_BASE_URL}/api/mulesoft/orders`, payload)
      .subscribe({
        next: (res) => {
          this.gatewayResponse = res;
          this.order = createEmptyOrder();
          this.showSuccessToast(`Purchase order ${payload.poNumber} submitted: ${payload.materialDescription}.`);
        },
        error: (err: unknown) => {
          console.error(err);
          this.clearSuccessToast();
          this.gatewayResponse = { error: 'Failed to dispatch payload to API Gateway endpoint.' };
        }
      });
  }

  public toggleWorkerState(): void {
    const targetState = !this.isWorkerRunning;
    const endpoint = targetState ? 'start' : 'stop';

    this.http.get<string>(`${API_BASE_URL}/api/worker/${endpoint}`)
      .subscribe({
        next: (res) => {
          this.isWorkerRunning = targetState;
          this.gatewayResponse = { systemAction: res };
        },
        error: (err: unknown) => {
          console.error(err);
          this.gatewayResponse = { error: 'Failed to dispatch state control command to backend worker.' };
        }
      });
  }

  private showSuccessToast(message: string): void {
    this.clearSuccessToast();
    this.successToastMessage = message;
    this.successToastTimeoutId = setTimeout(() => {
      this.successToastMessage = '';
      this.successToastTimeoutId = null;
    }, 3500);
  }

  private clearSuccessToast(): void {
    if (this.successToastTimeoutId) {
      clearTimeout(this.successToastTimeoutId);
      this.successToastTimeoutId = null;
    }

    this.successToastMessage = '';
  }
}

bootstrapApplication(AppComponent, {
  providers: [provideHttpClient()]
}).catch((err: unknown) => console.error(err));
