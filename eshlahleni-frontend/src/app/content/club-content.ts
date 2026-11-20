/** Confirmed club contact details; policy drafts remain clearly marked on their pages. */
export const clubContent = {
  name: "Es'hlahleni Social Club", established: '2025', motto: "KUPHOLIWE AK’TATAZELWA!", sampleContent: false,
  contact: {
    address: ['32 Albany Grove', 'Durban Central', 'Durban', '4001'],
    whatsAppNumber: '083 726 5910', whatsAppUrl: 'https://wa.me/27837265910',
    directionsUrl: 'https://www.google.com/maps/search/?api=1&query=32%20Albany%20Grove%2C%20Durban%20Central%2C%20Durban%2C%204001',
  },
  rules: [
    { title: '18+', text: 'You must be at least 18 to register.' },
    { title: 'Register online.', text: 'Share your details and consent to start your registration.' },
    { title: 'Under review.', text: 'An administrator reviews your application.' },
    { title: 'Welcome in.', text: 'Accepted applicants receive a membership number.' },
  ],
} as const;
