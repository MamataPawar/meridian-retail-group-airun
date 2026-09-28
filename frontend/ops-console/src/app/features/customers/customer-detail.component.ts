import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTableModule } from '@angular/material/table';
import { MatDividerModule } from '@angular/material/divider';
import { forkJoin, switchMap } from 'rxjs';
import { IdentityApiService, LoyaltyApiService, OrderApiService } from '../../core/api.services';

@Component({
  selector: 'app-customer-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, MatButtonModule, MatIconModule,
    MatProgressSpinnerModule, MatTabsModule, MatTableModule, MatDividerModule],
  template: `
    <div class="page-header">
      <div style="display:flex;align-items:center;gap:12px">
        <a routerLink="/customers" mat-icon-button><mat-icon>arrow_back</mat-icon></a>
        <h1>{{customer?.firstName}} {{customer?.lastName}}</h1>
      </div>
      <span class="badge green">Active</span>
    </div>

    <div *ngIf="loading" class="loading-spinner"><mat-spinner/></div>

    <div *ngIf="customer && !loading" style="display:grid;grid-template-columns:1fr 2fr;gap:16px">
      <!-- Profile card -->
      <div>
        <div class="card" style="margin-bottom:16px">
          <div style="text-align:center;padding-bottom:16px">
            <div style="width:72px;height:72px;border-radius:50%;background:#e0e7ff;display:flex;align-items:center;justify-content:center;margin:0 auto 12px;font-size:1.8rem;color:#4f46e5">
              {{customer.firstName?.charAt(0)}}{{customer.lastName?.charAt(0)}}
            </div>
            <div style="font-size:1.1rem;font-weight:600">{{customer.firstName}} {{customer.lastName}}</div>
            <div style="color:#6b7280;font-size:.85rem">{{customer.email}}</div>
            <div style="color:#6b7280;font-size:.85rem">{{customer.phone || 'No phone'}}</div>
            <div style="margin-top:8px"><span class="badge gray">{{customer.country}}</span></div>
          </div>
          <mat-divider/>
          <div style="margin-top:12px">
            <div class="detail-row"><span>Member Since</span><span>{{customer.createdAt | date:'mediumDate'}}</span></div>
            <div class="detail-row"><span>Customer ID</span><span style="font-family:monospace;font-size:.75rem">{{customer.id | slice:0:12}}…</span></div>
          </div>
        </div>

        <!-- Loyalty -->
        <div class="card" style="margin-bottom:16px" *ngIf="loyalty">
          <h3>Loyalty</h3>
          <div style="text-align:center;padding:8px 0">
            <div style="font-size:2rem;font-weight:700;color:#4f46e5">{{loyalty.points | number}}</div>
            <div style="font-size:.8rem;color:#6b7280">points</div>
            <div style="margin-top:8px">
              <span class="badge"
                [class.blue]="loyalty.tier==='bronze'"
                [class.gray]="loyalty.tier==='silver'"
                [class.yellow]="loyalty.tier==='gold'"
                [class.green]="loyalty.tier==='platinum'">
                {{loyalty.tier | titlecase}}
              </span>
            </div>
          </div>
        </div>

        <!-- Linked Identities -->
        <div class="card">
          <h3>Linked Identities</h3>
          <div *ngFor="let link of customer.identityLinks" class="identity-link">
            <div style="font-weight:500;font-size:.85rem">{{link.system}}</div>
            <div style="font-family:monospace;font-size:.75rem;color:#6b7280">{{link.externalId}}</div>
          </div>
          <div *ngIf="!customer.identityLinks?.length" style="color:#9ca3af;font-size:.85rem">No linked identities</div>
        </div>
      </div>

      <!-- Tabs: Orders, Consents, Merge Audit -->
      <div>
        <mat-tab-group>
          <mat-tab label="Recent Orders">
            <div class="card" style="margin-top:16px">
              <div *ngIf="!orders.length" style="color:#9ca3af;padding:16px 0">No orders found</div>
              <table mat-table [dataSource]="orders" *ngIf="orders.length">
                <ng-container matColumnDef="id">
                  <th mat-header-cell *matHeaderCellDef>ID</th>
                  <td mat-cell *matCellDef="let o">
                    <a [routerLink]="['/orders', o.id]" style="color:#4f46e5;font-family:monospace;font-size:.8rem">{{o.id | slice:0:8}}…</a>
                  </td>
                </ng-container>
                <ng-container matColumnDef="channel">
                  <th mat-header-cell *matHeaderCellDef>Channel</th>
                  <td mat-cell *matCellDef="let o"><span class="badge blue">{{o.channel}}</span></td>
                </ng-container>
                <ng-container matColumnDef="amount">
                  <th mat-header-cell *matHeaderCellDef>Amount</th>
                  <td mat-cell *matCellDef="let o">{{o.currency}} {{o.totalAmount | number:'1.2-2'}}</td>
                </ng-container>
                <ng-container matColumnDef="status">
                  <th mat-header-cell *matHeaderCellDef>Status</th>
                  <td mat-cell *matCellDef="let o">
                    <span class="badge"
                      [class.green]="o.status==='confirmed'||o.status==='delivered'"
                      [class.yellow]="o.status==='pending'"
                      [class.red]="o.status==='cancelled'">{{o.status}}</span>
                  </td>
                </ng-container>
                <ng-container matColumnDef="date">
                  <th mat-header-cell *matHeaderCellDef>Date</th>
                  <td mat-cell *matCellDef="let o" style="font-size:.8rem">{{o.createdAt | date:'shortDate'}}</td>
                </ng-container>
                <tr mat-header-row *matHeaderRowDef="['id','channel','amount','status','date']"></tr>
                <tr mat-row *matRowDef="let r; columns:['id','channel','amount','status','date'];"></tr>
              </table>
            </div>
          </mat-tab>

          <mat-tab label="Consents ({{customer.consents?.length || 0}})">
            <div class="card" style="margin-top:16px">
              <table mat-table [dataSource]="customer.consents || []">
                <ng-container matColumnDef="jurisdiction">
                  <th mat-header-cell *matHeaderCellDef>Jurisdiction</th>
                  <td mat-cell *matCellDef="let c"><span class="badge gray">{{c.jurisdiction}}</span></td>
                </ng-container>
                <ng-container matColumnDef="type">
                  <th mat-header-cell *matHeaderCellDef>Type</th>
                  <td mat-cell *matCellDef="let c">{{c.consentType}}</td>
                </ng-container>
                <ng-container matColumnDef="granted">
                  <th mat-header-cell *matHeaderCellDef>Status</th>
                  <td mat-cell *matCellDef="let c">
                    <span class="badge" [class.green]="c.granted" [class.red]="!c.granted">
                      {{c.granted ? 'Granted' : 'Withdrawn'}}
                    </span>
                  </td>
                </ng-container>
                <ng-container matColumnDef="at">
                  <th mat-header-cell *matHeaderCellDef>Recorded</th>
                  <td mat-cell *matCellDef="let c" style="font-size:.8rem">{{c.recordedAt | date:'short'}}</td>
                </ng-container>
                <tr mat-header-row *matHeaderRowDef="['jurisdiction','type','granted','at']"></tr>
                <tr mat-row *matRowDef="let r; columns:['jurisdiction','type','granted','at'];"></tr>
              </table>
            </div>
          </mat-tab>

          <mat-tab label="Merge Audit ({{customer.mergeAudits?.length || 0}})">
            <div class="card" style="margin-top:16px">
              <div *ngIf="!customer.mergeAudits?.length" style="color:#9ca3af;padding:16px 0">No merge history</div>
              <table mat-table [dataSource]="customer.mergeAudits || []" *ngIf="customer.mergeAudits?.length">
                <ng-container matColumnDef="merged">
                  <th mat-header-cell *matHeaderCellDef>Merged Customer</th>
                  <td mat-cell *matCellDef="let a" style="font-family:monospace;font-size:.75rem">{{a.mergedCustomerId | slice:0:12}}…</td>
                </ng-container>
                <ng-container matColumnDef="type">
                  <th mat-header-cell *matHeaderCellDef>Type</th>
                  <td mat-cell *matCellDef="let a">
                    <span class="badge" [class.blue]="a.mergeType==='deterministic'" [class.yellow]="a.mergeType==='probabilistic'">
                      {{a.mergeType}}
                    </span>
                  </td>
                </ng-container>
                <ng-container matColumnDef="reviewer">
                  <th mat-header-cell *matHeaderCellDef>Reviewer</th>
                  <td mat-cell *matCellDef="let a">{{a.reviewerId || 'auto'}}</td>
                </ng-container>
                <ng-container matColumnDef="at">
                  <th mat-header-cell *matHeaderCellDef>Merged At</th>
                  <td mat-cell *matCellDef="let a" style="font-size:.8rem">{{a.mergedAt | date:'short'}}</td>
                </ng-container>
                <ng-container matColumnDef="expires">
                  <th mat-header-cell *matHeaderCellDef>Undo Until</th>
                  <td mat-cell *matCellDef="let a" style="font-size:.8rem">
                    <span [class.red]="isExpired(a.expiresAt)">{{a.expiresAt | date:'shortDate'}}</span>
                  </td>
                </ng-container>
                <tr mat-header-row *matHeaderRowDef="['merged','type','reviewer','at','expires']"></tr>
                <tr mat-row *matRowDef="let r; columns:['merged','type','reviewer','at','expires'];"></tr>
              </table>
            </div>
          </mat-tab>

          <mat-tab label="Addresses">
            <div class="card" style="margin-top:16px">
              <div *ngFor="let addr of customer.addresses" class="address-card">
                <div *ngIf="addr.isDefault"><span class="badge green" style="margin-bottom:6px">Default</span></div>
                <div>{{addr.line1}}</div>
                <div *ngIf="addr.line2">{{addr.line2}}</div>
                <div>{{addr.city}}, {{addr.postalCode}}</div>
                <div>{{addr.country}}</div>
              </div>
              <div *ngIf="!customer.addresses?.length" style="color:#9ca3af">No addresses on file</div>
            </div>
          </mat-tab>
        </mat-tab-group>
      </div>
    </div>
  `,
  styles: [`
    h3 { margin: 0 0 12px; font-size: .9rem; font-weight: 600; color: #374151; }
    .detail-row {
      display: flex; justify-content: space-between; padding: 6px 0;
      font-size: .85rem; border-bottom: 1px solid #f3f4f6;
      &:last-child { border: none; }
    }
    .identity-link {
      padding: 8px 0; border-bottom: 1px solid #f3f4f6;
      &:last-child { border: none; }
    }
    .address-card {
      padding: 12px; background: #f9fafb; border-radius: 6px;
      margin-bottom: 8px; font-size: .9rem;
    }
    a { color: inherit; }
  `]
})
export class CustomerDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private identityApi = inject(IdentityApiService);
  private loyaltyApi = inject(LoyaltyApiService);
  private ordersApi = inject(OrderApiService);

  customer: any = null;
  loyalty: any = null;
  orders: any[] = [];
  loading = true;

  ngOnInit() {
    this.route.paramMap.pipe(
      switchMap(params => {
        const id = params.get('id')!;
        return forkJoin({
          customer: this.identityApi.getCustomer(id),
          orders: this.ordersApi.getOrders({ customerId: id }, 1, 10),
        });
      })
    ).subscribe({
      next: ({ customer, orders }) => {
        this.customer = customer;
        this.orders = orders.items;
        this.loading = false;
        this.loyaltyApi.getAccount(customer.id).subscribe({
          next: l => this.loyalty = l,
          error: () => {} // loyalty account may not exist
        });
      },
      error: () => { this.loading = false; }
    });
  }

  isExpired(date: string): boolean {
    return new Date(date) < new Date();
  }
}
