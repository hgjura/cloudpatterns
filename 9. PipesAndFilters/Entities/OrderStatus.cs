namespace PipesAndFilters
{
    public enum OrderStatus
    {
        Uknnown, //this is a status where the order is in a defective state
        New, //Order has been created and placed in the system
        PaymentPending, //Orders with Bank Deposit as payment option - still waiting for your payment
        PaymentReceived, //Orders with Bank Deposit as payment option - we have received your payment
        VerificationPending, //Order waiting for Finance verification
        VerificationInProcess, //Order verification in process
        CheckInProgress, //Order is undergoing finance verification
        Checked, //Order has passed finance verification and will be passed to our Warehouse team
        OrderReceiving, //Order was forwarded to our warehouse and will be prepared shortly!
        OrderBeingProcessed, //Order is getting picked and packed
        Shipped, //Order has been scheduled for delivery
        InDelivery, //Order is on the way to you today
        Delivered, //Order has been delivered to you!
        RefundBeingProcessed, //We have received your parcel back at our warehouse and are preparing your refund
        Refunded, //Refund has been credited via your original payment method or Wallet Credit
        Closed //Order has been canceled
    }
}
