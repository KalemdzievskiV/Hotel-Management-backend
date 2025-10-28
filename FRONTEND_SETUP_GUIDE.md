# 🚀 Frontend Setup Guide - Step by Step

## 📋 **Prerequisites Installation**

### **Step 1: Install Node.js**

**Download Node.js LTS (Long Term Support):**
- Visit: https://nodejs.org/
- Download the **LTS version** (currently 20.x.x)
- Run the installer
- ✅ Check "Automatically install necessary tools" during installation
- Restart your terminal/IDE after installation

**Verify Installation:**
```powershell
node --version  # Should show v20.x.x
npm --version   # Should show 10.x.x
```

---

## 🏗️ **Step 2: Create Next.js Project**

**Open PowerShell/Terminal in:** `C:\Users\vlada\RiderProjects`

```powershell
# Create Next.js app with TypeScript and Tailwind
npx create-next-app@latest hotel-management-frontend

# When prompted, choose:
✅ Would you like to use TypeScript? → Yes
✅ Would you like to use ESLint? → Yes
✅ Would you like to use Tailwind CSS? → Yes
✅ Would you like to use `src/` directory? → No
✅ Would you like to use App Router? → Yes
✅ Would you like to customize the default import alias (@/*)? → No
```

---

## 📦 **Step 3: Install Core Dependencies**

```powershell
# Navigate to frontend directory
cd hotel-management-frontend

# Install API & State Management
npm install axios @tanstack/react-query zustand

# Install Form Libraries
npm install react-hook-form zod @hookform/resolvers

# Install UI Utilities
npm install class-variance-authority clsx tailwind-merge lucide-react

# Install Date & Chart Libraries
npm install date-fns recharts

# Install shadcn/ui CLI
npx shadcn-ui@latest init
```

**When shadcn-ui init prompts:**
```
✅ Would you like to use TypeScript? → yes
✅ Which style would you like to use? → Default
✅ Which color would you like to use as base color? → Slate
✅ Where is your global CSS file? → app/globals.css
✅ Would you like to use CSS variables for colors? → yes
✅ Are you using a custom tailwind prefix? → no
✅ Where is your tailwind.config.js located? → tailwind.config.ts
✅ Configure the import alias for components? → @/components
✅ Configure the import alias for utils? → @/lib/utils
✅ Are you using React Server Components? → yes
```

---

## 🎨 **Step 4: Install shadcn/ui Components**

```powershell
# Install commonly used components
npx shadcn-ui@latest add button
npx shadcn-ui@latest add card
npx shadcn-ui@latest add input
npx shadcn-ui@latest add label
npx shadcn-ui@latest add select
npx shadcn-ui@latest add dialog
npx shadcn-ui@latest add dropdown-menu
npx shadcn-ui@latest add table
npx shadcn-ui@latest add form
npx shadcn-ui@latest add toast
npx shadcn-ui@latest add badge
npx shadcn-ui@latest add calendar
npx shadcn-ui@latest add popover
npx shadcn-ui@latest add separator
npx shadcn-ui@latest add tabs
npx shadcn-ui@latest add avatar
npx shadcn-ui@latest add checkbox
npx shadcn-ui@latest add radio-group
npx shadcn-ui@latest add switch
npx shadcn-ui@latest add textarea
```

---

## ⚙️ **Step 5: Create Environment Variables**

Create `.env.local` in the frontend root:

```env
# API Configuration
NEXT_PUBLIC_API_URL=https://localhost:5001/api
NEXT_PUBLIC_API_TIMEOUT=30000

# App Configuration
NEXT_PUBLIC_APP_NAME=Hotel Management System
NEXT_PUBLIC_APP_VERSION=1.0.0
```

---

## 📁 **Step 6: Project Structure Setup**

Your project structure should look like this:

```
hotel-management-frontend/
├── app/
│   ├── (auth)/
│   │   ├── login/
│   │   │   └── page.tsx
│   │   └── register/
│   │       └── page.tsx
│   ├── (dashboard)/
│   │   ├── layout.tsx
│   │   ├── page.tsx
│   │   └── hotels/
│   │       └── page.tsx
│   ├── layout.tsx
│   ├── globals.css
│   └── providers.tsx
├── components/
│   ├── ui/              # shadcn components
│   ├── layout/
│   ├── forms/
│   └── shared/
├── lib/
│   ├── api/
│   ├── hooks/
│   ├── utils/
│   └── constants.ts
├── types/
│   ├── hotel.ts
│   ├── room.ts
│   ├── guest.ts
│   └── reservation.ts
├── store/
│   └── authStore.ts
├── .env.local
├── next.config.js
├── tailwind.config.ts
├── tsconfig.json
└── package.json
```

---

## 🚀 **Step 7: Run Development Server**

```powershell
npm run dev
```

Open: http://localhost:3000

---

## ✅ **Step 8: Verify Setup**

**Check these files exist:**
- ✅ `components/ui/button.tsx`
- ✅ `components/ui/card.tsx`
- ✅ `lib/utils.ts`
- ✅ `tailwind.config.ts`
- ✅ `.env.local`

**Check package.json dependencies:**
```json
{
  "dependencies": {
    "next": "^14.0.0",
    "react": "^18.2.0",
    "axios": "^1.6.0",
    "@tanstack/react-query": "^5.0.0",
    "zustand": "^4.4.0",
    "react-hook-form": "^7.48.0",
    "zod": "^3.22.0",
    ...
  }
}
```

---

## 🎯 **What I'll Create for You**

Once the setup is complete, I'll create:

### **Phase 1: Foundation**
1. ✅ TypeScript types (matching your backend)
2. ✅ API client with Axios
3. ✅ Auth store with Zustand
4. ✅ Auth pages (Login/Register)
5. ✅ Protected route middleware
6. ✅ Dashboard layout (Sidebar + Header)

### **Phase 2: Core Pages**
7. ✅ Dashboard home with statistics
8. ✅ Hotels management (List, Create, Edit)
9. ✅ Rooms management
10. ✅ Basic navigation

---

## 📝 **Quick Commands Reference**

```powershell
# Development
npm run dev              # Start dev server
npm run build            # Build for production
npm run start            # Start production server
npm run lint             # Run ESLint

# Install new package
npm install package-name

# Add shadcn component
npx shadcn-ui@latest add component-name
```

---

## 🐛 **Troubleshooting**

### **Issue: npx not found**
- Restart terminal after Node.js installation
- Or use: `npm install -g npx`

### **Issue: Port 3000 already in use**
```powershell
# Use different port
npm run dev -- -p 3001
```

### **Issue: Module not found**
```powershell
# Clear cache and reinstall
rm -rf node_modules package-lock.json
npm install
```

---

## 🔗 **Useful Resources**

- **Next.js Docs:** https://nextjs.org/docs
- **shadcn/ui:** https://ui.shadcn.com/
- **TanStack Query:** https://tanstack.com/query/latest
- **Tailwind CSS:** https://tailwindcss.com/docs
- **React Hook Form:** https://react-hook-form.com/

---

## ✅ **Next Steps**

1. ✅ Install Node.js
2. ✅ Create Next.js project
3. ✅ Install dependencies
4. ✅ Setup shadcn/ui
5. ✅ Create .env.local
6. ✅ Run `npm run dev`
7. ✅ Let me know when ready - I'll create all the code!

---

**Once you complete the setup, I'll scaffold the entire frontend with:**
- Complete TypeScript types
- API client layer
- Authentication system
- Dashboard with sidebar
- First few pages ready to use

**Ready to start!** 🚀
